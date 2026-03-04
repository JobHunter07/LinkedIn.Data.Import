using LinkedIn.Data.Import;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinkedIn.Data.Import.Cli.Pipeline;

/// <summary>
/// Builds the DI host for the import operation.
/// Extracted from Program.cs to keep it clean.
/// </summary>
internal static class HostBuilder
{
    internal static IHost BuildHost(ImportOptions options)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                // Suppress framework noise — Spectre.Console owns the terminal.
                logging.ClearProviders();
            })
            .ConfigureServices((_, services) =>
            {
                // Register connection factory so it can be injected into services like ImportHostedService
                Func<System.Data.IDbConnection> connectionFactory = () => new SqlConnection(options.ConnectionString);
                services.AddSingleton(connectionFactory);

                services.AddLinkedInImporter(
                    connectionFactory: _ => connectionFactory(),
                    dialectFactory: _ => new SqlServerDialect());

                // Override IEventDispatcher lifetime to Singleton so that ImportHostedService
                // and the ILinkedInImporter factory both resolve the same dispatcher instance,
                // allowing the CLI to subscribe its progress handler before ImportAsync runs.
                services.Replace(
                    ServiceDescriptor.Singleton<IEventDispatcher, InProcessEventDispatcher>());

                // Make ImportOptions available for injection into ImportHostedService.
                services.AddSingleton(options);

                services.AddHostedService<ImportHostedService>();
            })
            .Build();
    }
}

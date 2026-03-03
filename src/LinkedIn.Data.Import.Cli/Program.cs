using LinkedIn.Data.Import;
using LinkedIn.Data.Import.Cli;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Phase 1: Collect options via interactive wizard ─────────────────────────
ImportOptions options = ImportWizard.Ask();

// ── Phase 2: Build the generic host with the collected connection string ─────
using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        // Suppress framework noise — Spectre.Console owns the terminal.
        logging.ClearProviders();
    })
    .ConfigureServices((_, services) =>
    {
        services.AddLinkedInImporter(
            connectionFactory: _ => new SqlConnection(options.ConnectionString),
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

// ── Phase 3: Run the host (ImportHostedService drives the import and stops the host) ─
await host.RunAsync();
return Environment.ExitCode;

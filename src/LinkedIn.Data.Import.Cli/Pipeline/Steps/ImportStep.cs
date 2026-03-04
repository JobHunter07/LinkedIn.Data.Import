using LinkedIn.Data.Import.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkedIn.Data.Import.Cli.Pipeline.Steps;

/// <summary>
/// Step 2: Build and run the import host.
/// The existing LinkedInImporter handles ZIP discovery, extraction, and CSV import.
/// </summary>
public sealed class ImportStep : IPipelineStep
{
    public async Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create ImportOptions pointing to the original ZIP directory
            var options = new ImportOptions
            {
                ZipRootDirectory = context.ZipRootDirectory,
                ConnectionString = context.ConnectionString
            };

            // Build and run the host - the LinkedInImporter will handle everything
            using var host = HostBuilder.BuildHost(options);
            await host.RunAsync(cancellationToken);

            context.ExitCode = Environment.ExitCode;
            return context;
        }
        catch (Exception ex)
        {
            context.IsSuccess = false;
            context.ErrorMessage = $"Import failed: {ex.Message}";
            context.ExitCode = 1;
            return context;
        }
    }
}

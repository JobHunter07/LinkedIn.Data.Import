using LinkedIn.Data.Import.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkedIn.Data.Import.Cli.Pipeline.Steps;

/// <summary>
/// Step 4: Build and run the import host using the hosted service.
/// </summary>
public sealed class ImportStep : IPipelineStep
{
    public async Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create ImportOptions from context
            var options = new ImportOptions
            {
                ZipRootDirectory = context.ExtractionDirectory,
                ConnectionString = context.ConnectionString
            };

            // Build and run the host (existing logic from Program.cs)
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

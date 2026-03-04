using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli.Pipeline.Steps;

/// <summary>
/// Step 1: Collect configuration (ZIP directory, connection string).
/// </summary>
public sealed class ConfigurationStep : IPipelineStep
{
    private readonly IConfiguration _config;

    public ConfigurationStep(IConfiguration config)
    {
        _config = config;
    }

    public async Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = await ImportWizard.AskAsync(_config);
            
            context.ZipRootDirectory = options.ZipRootDirectory;
            context.ConnectionString = options.ConnectionString;
            
            return context;
        }
        catch (Exception ex)
        {
            context.IsSuccess = false;
            context.ErrorMessage = $"Configuration failed: {ex.Message}";
            context.ExitCode = 1;
            return context;
        }
    }
}

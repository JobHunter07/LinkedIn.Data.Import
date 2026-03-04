using Spectre.Console;

namespace LinkedIn.Data.Import.Cli.Pipeline.Steps;

/// <summary>
/// Step 2: Extract LinkedIn ZIP archives to get CSVs.
/// </summary>
public sealed class ExtractionStep : IPipelineStep
{
    public async Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var extractionResult = await ZipExtractionHelper.ExtractZipsAsync(context.ZipRootDirectory);

            if (!extractionResult.IsSuccess)
            {
                context.IsSuccess = false;
                context.ErrorMessage = $"Failed to extract ZIP files: {extractionResult.ErrorMessage}";
                context.ExitCode = 1;
                AnsiConsole.MarkupLine($"[red]✗ {context.ErrorMessage}[/]");
                return context;
            }

            context.ExtractionDirectory = extractionResult.ExtractionDirectory;
            context.ExtractedFiles = extractionResult.ExtractedFiles;

            return context;
        }
        catch (Exception ex)
        {
            context.IsSuccess = false;
            context.ErrorMessage = $"Extraction failed: {ex.Message}";
            context.ExitCode = 1;
            return context;
        }
    }
}

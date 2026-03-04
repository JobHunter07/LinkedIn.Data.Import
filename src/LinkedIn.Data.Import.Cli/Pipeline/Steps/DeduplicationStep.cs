using Spectre.Console;

namespace LinkedIn.Data.Import.Cli.Pipeline.Steps;

/// <summary>
/// Step 3: Optionally deduplicate extracted CSV files.
/// Uses the extraction directory from context - no need to ask user for path.
/// </summary>
public sealed class DeduplicationStep : IPipelineStep
{
    public async Task<ImportContext> ExecuteAsync(ImportContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var csvFiles = context.ExtractedFiles
                .Where(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (csvFiles.Count == 0)
            {
                // No CSVs to deduplicate, skip this step
                return context;
            }

            var wantsDedupe = AnsiConsole.Confirm(
                "[bold]Would you like to deduplicate CSV files before importing?[/]",
                defaultValue: false);

            if (!wantsDedupe)
            {
                return context;
            }

            // Run deduplication with the extraction directory
            await CsvDeduplicationWizard.RunWithDirectoryAsync(context.ExtractionDirectory);
            
            AnsiConsole.WriteLine();

            var continueImport = AnsiConsole.Confirm(
                "[bold]Continue with import?[/]",
                defaultValue: true);

            if (!continueImport)
            {
                context.IsSuccess = false;
                context.ErrorMessage = "Import cancelled by user";
                context.ExitCode = 0;
                AnsiConsole.MarkupLine("[yellow]Import cancelled.[/]");
            }

            return context;
        }
        catch (Exception ex)
        {
            context.IsSuccess = false;
            context.ErrorMessage = $"Deduplication failed: {ex.Message}";
            context.ExitCode = 1;
            return context;
        }
    }
}

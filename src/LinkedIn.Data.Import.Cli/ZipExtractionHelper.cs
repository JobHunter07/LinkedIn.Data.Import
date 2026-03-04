using LinkedIn.Data.Import.Features.ZipIngestion;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Result of ZIP extraction operation.
/// </summary>
public sealed class ExtractionResult
{
    public bool IsSuccess { get; init; }
    public string ExtractionDirectory { get; init; } = string.Empty;
    public List<string> ExtractedFiles { get; init; } = [];
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Helper to extract LinkedIn ZIP files before import.
/// </summary>
internal static class ZipExtractionHelper
{
    internal static async Task<ExtractionResult> ExtractZipsAsync(string zipDirectory)
    {
        try
        {
            // Find ZIP files
            var discovery = new ZipDiscovery();
            var discoveryResult = discovery.Discover(zipDirectory);

            if (!discoveryResult.IsSuccess)
            {
                // If no archives found, check for CSVs directly
                if (discoveryResult.ErrorCode == LinkedIn.Data.Import.Shared.ErrorCode.NoArchivesFound)
                {
                    AnsiConsole.MarkupLine("[yellow]No ZIP files found. Looking for CSV files directly...[/]");
                    return new ExtractionResult
                    {
                        IsSuccess = true,
                        ExtractionDirectory = zipDirectory,
                        ExtractedFiles = Directory.GetFiles(zipDirectory, "*.csv", SearchOption.AllDirectories).ToList()
                    };
                }

                return new ExtractionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to discover ZIP files: {discoveryResult.ErrorMessage}"
                };
            }

            var archives = discoveryResult.Value.Archives;

            if (archives.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No ZIP files found. Looking for CSV files directly...[/]");
                return new ExtractionResult
                {
                    IsSuccess = true,
                    ExtractionDirectory = zipDirectory,
                    ExtractedFiles = Directory.GetFiles(zipDirectory, "*.csv", SearchOption.AllDirectories).ToList()
                };
            }

            AnsiConsole.MarkupLine($"[cyan]Found {archives.Count} ZIP archive(s).[/]");

            // Create extraction directory
            var extractionRoot = Path.Combine(Path.GetTempPath(), "LinkedInImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionRoot);

            var extractor = new ZipExtractor();
            var allExtractedFiles = new List<string>();

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle(Style.Parse("blue bold"))
                .StartAsync("[blue]Extracting ZIP archives...[/]", async ctx =>
                {
                    foreach (var archive in archives)
                    {
                        ctx.Status = $"[blue]Extracting {Path.GetFileName(archive.FilePath)}...[/]";
                        
                        var extractResult = extractor.Extract(archive, extractionRoot);
                        
                        if (extractResult.IsSuccess)
                        {
                            allExtractedFiles.AddRange(extractResult.Value);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]⚠ {extractResult.ErrorMessage}[/]");
                        }
                        
                        await Task.Delay(100); // Small delay for UI feedback
                    }
                });

            var csvFiles = allExtractedFiles.Where(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).ToList();
            
            AnsiConsole.MarkupLine($"[green]✓ Extracted {csvFiles.Count} CSV file(s).[/]");
            AnsiConsole.WriteLine();

            return new ExtractionResult
            {
                IsSuccess = true,
                ExtractionDirectory = extractionRoot,
                ExtractedFiles = allExtractedFiles
            };
        }
        catch (Exception ex)
        {
            return new ExtractionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

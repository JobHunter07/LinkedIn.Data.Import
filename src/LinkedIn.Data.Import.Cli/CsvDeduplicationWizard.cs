using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.ImportTracking;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Interactive utility to deduplicate CSV files safely without modifying originals.
/// </summary>
internal static class CsvDeduplicationWizard
{
    /// <summary>
    /// Runs the deduplication wizard, prompting for paths.
    /// </summary>
    internal static Task RunAsync() => RunWithDirectoryAsync(null);

    /// <summary>
    /// Runs the deduplication wizard using a pre-determined directory.
    /// </summary>
    /// <param name="csvDirectory">Directory containing CSVs (if null, will prompt user)</param>
    internal static async Task RunWithDirectoryAsync(string? csvDirectory)
    {
        // Banner
        AnsiConsole.Write(
            new FigletText("CSV Deduplicator")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[dim]Safely remove duplicate rows from CSV files without modifying originals.[/]");
        AnsiConsole.WriteLine();

        // Get input path (file or directory) - use provided directory or prompt
        string inputPath;

        if (!string.IsNullOrWhiteSpace(csvDirectory))
        {
            inputPath = csvDirectory;
            AnsiConsole.MarkupLine($"[cyan]Using directory: {Markup.Escape(csvDirectory)}[/]");
        }
        else
        {
            inputPath = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Path to CSV file or directory:[/]")
                    .PromptStyle("cyan")
                    .Validate(path =>
                    {
                        if (File.Exists(path) && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                            return ValidationResult.Success();

                        if (Directory.Exists(path))
                            return ValidationResult.Success();

                        return ValidationResult.Error(
                            "[red]Path must be a CSV file or directory[/]");
                    }));
        }

        // Get output directory
        var outputDir = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Output directory for deduplicated files:[/]")
                .PromptStyle("cyan")
                .DefaultValue(Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory())
                .Validate(path =>
                {
                    try
                    {
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);
                        return ValidationResult.Success();
                    }
                    catch (Exception ex)
                    {
                        return ValidationResult.Error($"[red]Cannot create directory:[/] {ex.Message}");
                    }
                }));

        AnsiConsole.WriteLine();

        // Collect files to process
        var filesToProcess = File.Exists(inputPath)
            ? [inputPath]
            : Directory.GetFiles(inputPath, "*.csv", SearchOption.TopDirectoryOnly);

        if (filesToProcess.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No CSV files found.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[cyan]Found {filesToProcess.Length} CSV file(s) to process.[/]");
        AnsiConsole.WriteLine();

        var deduplicator = new CsvDeduplicator(new RowHasher());
        var results = new List<DeduplicationResult>();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Deduplicating files...[/]", maxValue: filesToProcess.Length);

                foreach (var file in filesToProcess)
                {
                    task.Description = $"[cyan]Processing {Path.GetFileName(file)}...[/]";
                    
                    var result = await deduplicator.DeduplicateAsync(file, outputDir);
                    results.Add(result);
                    
                    task.Increment(1);
                }
            });

        // Display results
        AnsiConsole.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("File")
            .AddColumn("Status", column => column.Centered())
            .AddColumn("Total Rows", column => column.RightAligned())
            .AddColumn("Unique", column => column.RightAligned())
            .AddColumn("Duplicates Removed", column => column.RightAligned());

        foreach (var result in results)
        {
            var fileName = Path.GetFileName(result.OriginalFilePath);
            var status = result.IsSuccess ? "[green]✓[/]" : "[red]✗[/]";
            
            if (result.IsSuccess)
            {
                table.AddRow(
                    fileName,
                    status,
                    result.TotalRows.ToString("N0"),
                    result.UniqueRows.ToString("N0"),
                    $"[yellow]{result.DuplicatesRemoved:N0}[/]");
            }
            else
            {
                table.AddRow(
                    fileName,
                    status,
                    "[red]Error[/]",
                    "-",
                    "-");
            }
        }

        AnsiConsole.Write(table);

        // Summary
        var totalSuccess = results.Count(r => r.IsSuccess);
        var totalDuplicates = results.Where(r => r.IsSuccess).Sum(r => r.DuplicatesRemoved);

        AnsiConsole.WriteLine();
        if (totalSuccess == results.Count)
        {
            AnsiConsole.MarkupLine($"[green]✓ Successfully processed {totalSuccess} file(s).[/]");
            AnsiConsole.MarkupLine($"[yellow]• Total duplicates removed: {totalDuplicates:N0}[/]");
            AnsiConsole.MarkupLine($"[dim]• Deduplicated files saved to: {outputDir}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ Processed {totalSuccess} of {results.Count} file(s) successfully.[/]");
            
            foreach (var error in results.Where(r => !r.IsSuccess))
            {
                AnsiConsole.MarkupLine($"[red]✗ {Path.GetFileName(error.OriginalFilePath)}: {error.ErrorMessage}[/]");
            }
        }
    }
}

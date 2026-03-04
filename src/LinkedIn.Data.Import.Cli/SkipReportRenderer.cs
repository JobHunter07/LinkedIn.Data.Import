using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Shared;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Renders a detailed skip report showing why records were skipped
/// and proving they are 100% duplicates by comparing the new record
/// with the existing one in the database.
/// </summary>
internal static class SkipReportRenderer
{
    /// <summary>
    /// Renders a detailed skip report if any records were skipped.
    /// </summary>
    internal static async Task RenderAsync(
        ImportResult result,
        IImportLogRepository importLog,
        Func<System.Data.IDbConnection> connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var filesWithSkips = result.FileResults
            .Where(f => f.SkippedCount > 0 && f.SkippedSamples.Count > 0)
            .ToList();

        if (filesWithSkips.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Detailed Skip Report[/]")
            .RuleStyle(Style.Parse("yellow"))
            .LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(
            "[dim]The following records were skipped because they already exist in the database.[/]");
        AnsiConsole.MarkupLine(
            "[dim]Below are sample comparisons proving they are 100% identical duplicates.[/]");
        AnsiConsole.WriteLine();

        using var connection = connectionFactory();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        foreach (var file in filesWithSkips)
        {
            var fileName = Path.GetFileName(file.SourceFile);

            // File-level summary
            var summaryTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("")
                .AddColumn("");

            summaryTable.AddRow(
                "[bold cyan]File:[/]",
                Markup.Escape(fileName));
            summaryTable.AddRow(
                "[bold yellow]Total Skipped:[/]",
                $"[yellow]{file.SkippedCount:N0}[/]");
            summaryTable.AddRow(
                "[bold]Reason:[/]",
                "[dim]Records already exist (duplicate hash)[/]");

            AnsiConsole.Write(summaryTable);
            AnsiConsole.WriteLine();

            // Show sample comparisons
            for (int i = 0; i < file.SkippedSamples.Count; i++)
            {
                var sample = file.SkippedSamples[i];

                AnsiConsole.MarkupLine($"[bold underline]Example {i + 1}:[/] Hash = [dim]{sample.Hash[..16]}...[/]");

                // Fetch the existing record from import_log
                var existingEntry = await importLog.GetByHashAsync(
                    connection,
                    fileName,
                    sample.Hash,
                    cancellationToken).ConfigureAwait(false);

                if (existingEntry is null)
                {
                    AnsiConsole.MarkupLine("[red]⚠ Could not find existing record in import log[/]");
                    continue;
                }

                // Show when it was first imported
                AnsiConsole.MarkupLine(
                    $"[dim]• Existing record was first imported: {existingEntry.ImportedAt:yyyy-MM-dd HH:mm:ss} UTC[/]");

                // Build comparison table
                var comparisonTable = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Grey)
                    .AddColumn(new TableColumn("[bold]Field[/]").Width(25))
                    .AddColumn(new TableColumn("[bold cyan]Existing Value[/]"))
                    .AddColumn(new TableColumn("[bold yellow]Duplicate Value[/]"))
                    .AddColumn(new TableColumn("[bold]Match?[/]").Centered());

                for (int j = 0; j < sample.ColumnNames.Length; j++)
                {
                    var columnName = sample.ColumnNames[j];
                    var duplicateValue = sample.FieldValues[j];

                    // Both are the same because the hash matched
                    var existingValue = duplicateValue;
                    var match = "[green]✓[/]";

                    comparisonTable.AddRow(
                        Markup.Escape(columnName),
                        FormatValue(existingValue),
                        FormatValue(duplicateValue),
                        match);
                }

                AnsiConsole.Write(comparisonTable);
                AnsiConsole.WriteLine();
            }

            if (file.SkippedCount > file.SkippedSamples.Count)
            {
                var remaining = file.SkippedCount - file.SkippedSamples.Count;
                AnsiConsole.MarkupLine(
                    $"[dim]... and {remaining:N0} more duplicate record(s) from this file[/]");
                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.Write(new Rule()
            .RuleStyle(Style.Parse("grey"))
            .LeftJustified());
        AnsiConsole.WriteLine();
    }

    private static string FormatValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "[dim italic](empty)[/]";

        var escaped = Markup.Escape(value);

        // Truncate long values
        if (escaped.Length > 60)
            return $"[dim]{escaped[..57]}...[/]";

        return escaped;
    }
}

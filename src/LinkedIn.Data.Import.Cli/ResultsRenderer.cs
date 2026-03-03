using LinkedIn.Data.Import.Shared;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Renders the import results summary — a per-file table with totals,
/// an overall status line, and an error panel when errors are present.
/// </summary>
internal static class ResultsRenderer
{
    internal static void Render(ImportResult result)
    {
        AnsiConsole.WriteLine();

        // ── Task 5.1: Per-file results table ─────────────────────────────────
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]File[/]"))
            .AddColumn(new TableColumn("[bold]Inserted[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Skipped[/]").RightAligned());

        foreach (var file in result.FileResults)
        {
            var statusMark = file.IsSuccess ? "[green]✓[/]" : "[red]✗[/]";
            table.AddRow(
                $"{statusMark} {Markup.Escape(Path.GetFileName(file.SourceFile))}",
                $"[green]{file.InsertedCount}[/]",
                $"[yellow]{file.SkippedCount}[/]");
        }

        // ── Task 5.2: Totals row ──────────────────────────────────────────────
        table.AddEmptyRow();
        table.AddRow(
            new Markup("[bold]TOTAL[/]"),
            new Markup($"[bold green]{result.TotalInserted}[/]"),
            new Markup($"[bold yellow]{result.TotalSkipped}[/]"));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Overall status line
        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine("[bold green]✓ Import completed successfully.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[bold red]✗ Import completed with errors.[/]");
        }

        // ── Tasks 5.3 & 5.4: Error / warning panel ───────────────────────────
        if (result.Errors.Count > 0)
        {
            AnsiConsole.WriteLine();

            var lines = result.Errors.Select(e =>
                $"[yellow][[{e.Code}]][/] [dim]{Markup.Escape(e.SourceFile)}[/] — {Markup.Escape(e.Message)}");

            AnsiConsole.Write(
                new Panel(string.Join(Environment.NewLine, lines))
                    .Header("[red bold] Errors & Warnings [/]")
                    .BorderColor(Color.Red)
                    .Expand());
        }

        AnsiConsole.WriteLine();
    }
}

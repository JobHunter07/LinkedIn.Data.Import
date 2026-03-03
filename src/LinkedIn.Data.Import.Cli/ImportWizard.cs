using LinkedIn.Data.Import.Shared;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Interactive wizard that collects <see cref="ImportOptions"/> from the user
/// using Spectre.Console prompts before the host is built.
/// </summary>
internal static class ImportWizard
{
    internal static ImportOptions Ask()
    {
        // Banner
        AnsiConsole.Write(
            new FigletText("LinkedIn Import")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[dim]Import your LinkedIn data export ZIP archives into a SQL Server database.[/]");
        AnsiConsole.WriteLine();

        // ── ZIP root directory ──────────────────────────────────────────────
        var zipDir = AnsiConsole.Prompt(
            new TextPrompt<string>(
                    "[bold]Path to directory containing LinkedIn ZIP exports:[/]")
                .PromptStyle("cyan")
                .Validate(path =>
                    Directory.Exists(path)
                        ? ValidationResult.Success()
                        : ValidationResult.Error(
                            $"[red]Directory not found:[/] {Markup.Escape(path)}")));

        // ── Connection string ───────────────────────────────────────────────
        var connectionString = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]SQL Server connection string:[/]")
                .PromptStyle("cyan")
                .DefaultValue(
                    "Server=.;Database=LinkedInData;Trusted_Connection=True;TrustServerCertificate=True;"));

        AnsiConsole.WriteLine();

        return new ImportOptions
        {
            ZipRootDirectory = zipDir,
            ConnectionString = connectionString,
        };
    }
}

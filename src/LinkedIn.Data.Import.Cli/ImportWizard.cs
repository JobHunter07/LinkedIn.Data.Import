using LinkedIn.Data.Import.Shared;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Interactive wizard that collects <see cref="ImportOptions"/> from the user
/// using Spectre.Console prompts before the host is built.
/// </summary>
internal static class ImportWizard
{
    internal static async Task<ImportOptions> AskAsync(Microsoft.Extensions.Configuration.IConfiguration config)
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

        // ── Option to deduplicate CSVs first ────────────────────────────────────
        var wantsDedupe = AnsiConsole.Confirm(
            "[bold]Would you like to deduplicate CSV files before importing?[/]",
            defaultValue: false);

        if (wantsDedupe)
        {
            await CsvDeduplicationWizard.RunAsync();
            AnsiConsole.WriteLine();

            var continueImport = AnsiConsole.Confirm(
                "[bold]Continue with import using deduplicated files?[/]",
                defaultValue: true);

            if (!continueImport)
            {
                AnsiConsole.MarkupLine("[yellow]Import cancelled.[/]");
                Environment.Exit(0);
            }
        }

        // ── ZIP root directory ──────────────────────────────────────────────
        var defaultZipDir = ImportDefaultsProvider.GetDefaultZipRootDirectory(config);

        var zipDirPrompt = new TextPrompt<string>(
                "[bold]Path to directory containing LinkedIn ZIP exports:[/]")
            .PromptStyle("cyan")
            .AllowEmpty()
            .Validate(path =>
            {
                // Allow empty if we have a default
                if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(defaultZipDir))
                    return ValidationResult.Success();

                return Directory.Exists(path)
                    ? ValidationResult.Success()
                    : ValidationResult.Error(
                        $"[red]Directory not found:[/] {Markup.Escape(path)}");
            });

        if (!string.IsNullOrWhiteSpace(defaultZipDir))
        {
            zipDirPrompt.DefaultValue(defaultZipDir);
        }

        var zipDir = AnsiConsole.Prompt(zipDirPrompt);

        // Use default if user pressed Enter without typing
        if (string.IsNullOrWhiteSpace(zipDir))
            zipDir = defaultZipDir!;

        // ── Connection string ───────────────────────────────────────────────
        var defaultConnection = ImportDefaultsProvider.GetDefaultConnectionString(config)
            ?? "Server=.;Database=LinkedInData;Trusted_Connection=True;TrustServerCertificate=True;";

        var connectionString = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]SQL Server connection string:[/]")
                .PromptStyle("cyan")
                .DefaultValue(defaultConnection));

        // ── Detect changes and prompt to save only if values changed ──────────
        var zipDirChanged = !string.Equals(zipDir, defaultZipDir, StringComparison.OrdinalIgnoreCase);
        var connectionChanged = !string.Equals(connectionString, defaultConnection, StringComparison.Ordinal);

        // Only prompt to save connection string if it changed
        if (connectionChanged)
        {
            var saveConnection = AnsiConsole.Confirm("Save connection string to user secrets for future runs?");

            if (saveConnection)
            {
                try
                {
                    UserSecretsManager.WriteSecret("Import:ConnectionString", connectionString);
                    AnsiConsole.MarkupLine("[green]Saved connection string to user secrets.[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to save user secret:[/] {ex.Message}");
                }
            }
        }

        // Only prompt to save ZIP directory if it changed
        if (zipDirChanged)
        {
            var saveZipDir = AnsiConsole.Confirm("Save ZIP directory path to appsettings for future runs?");

            if (saveZipDir)
            {
                try
                {
                    AppsettingsManager.UpdateZipRootDirectory(zipDir);
                    AnsiConsole.MarkupLine("[green]Saved ZIP directory path to appsettings.json.[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to save to appsettings.json:[/] {ex.Message}");
                }
            }
        }

        AnsiConsole.WriteLine();

        return new ImportOptions
        {
            ZipRootDirectory = zipDir,
            ConnectionString = connectionString,
        };
    }
}

using LinkedIn.Data.Import.Shared;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Application controller that manages the main menu loop and coordinates all operations.
/// </summary>
internal sealed class ApplicationController
{
    private readonly SettingsManager _settingsManager;
    private readonly ImportOrchestrator _importOrchestrator;

    public ApplicationController(SettingsManager settingsManager, ImportOrchestrator importOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);
        ArgumentNullException.ThrowIfNull(importOrchestrator);

        _settingsManager = settingsManager;
        _importOrchestrator = importOrchestrator;
    }

    /// <summary>
    /// Runs the main application loop, displaying the menu and handling user selections.
    /// </summary>
    /// <returns>Exit code: 0 for normal exit</returns>
    public async Task<int> RunAsync()
    {
        while (true)
        {
            try
            {
                // Check if settings are configured
                var hasSettings = _settingsManager.HasValidSettings();

                // Show menu and get selection
                var selection = MainMenu.Show(hasSettings);

                switch (selection)
                {
                    case MainMenu.MenuOption.Settings:
                        await HandleSettingsAsync();
                        break;

                    case MainMenu.MenuOption.DedupCsvs:
                        await HandleDedupAsync();
                        break;

                    case MainMenu.MenuOption.LinkedInImport:
                        await HandleImportAsync();
                        break;

                    case MainMenu.MenuOption.Exit:
                        AnsiConsole.MarkupLine("[dim]Goodbye![/]");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                MainMenu.ShowCompletionMessage("An error occurred", success: false);
            }
        }
    }

    /// <summary>
    /// Handles the Settings menu option.
    /// </summary>
    private async Task HandleSettingsAsync()
    {
        var options = await _settingsManager.GetOrConfigureSettingsAsync(forceReconfigure: true);

        if (options is not null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ Settings saved successfully![/]");
            AnsiConsole.WriteLine();

            _settingsManager.DisplayCurrentSettings();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to return to main menu...[/]");
            Console.ReadKey(true);
        }
        else
        {
            MainMenu.ShowCompletionMessage("Settings configuration cancelled", success: false);
        }
    }

    /// <summary>
    /// Handles the Dedup CSVs menu option.
    /// </summary>
    private async Task HandleDedupAsync()
    {
        var options = _settingsManager.GetCurrentSettings();

        if (options is null)
        {
            MainMenu.ShowCompletionMessage("Please configure settings first", success: false);
            return;
        }

        MainMenu.ShowBanner();

        AnsiConsole.Write(
            new Rule("[bold blue]CSV Deduplication[/]")
                .RuleStyle("blue")
                .LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[yellow]CSV deduplication feature coming soon![/]");
        AnsiConsole.MarkupLine("[dim]This will allow you to deduplicate CSV files extracted from ZIP archives[/]");
        AnsiConsole.MarkupLine("[dim]without importing them into the database.[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to return to main menu...[/]");
        Console.ReadKey(true);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles the LinkedIn Import menu option.
    /// </summary>
    private async Task HandleImportAsync()
    {
        var options = _settingsManager.GetCurrentSettings();

        if (options is null)
        {
            MainMenu.ShowCompletionMessage("Please configure settings first", success: false);
            return;
        }

        var exitCode = await _importOrchestrator.RunImportAsync(options);

        if (exitCode != 0)
        {
            MainMenu.ShowCompletionMessage("Import completed with errors", success: false);
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to return to main menu...[/]");
            Console.ReadKey(true);
        }
    }
}

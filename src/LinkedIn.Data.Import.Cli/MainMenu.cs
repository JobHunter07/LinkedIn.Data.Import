using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Main menu for the LinkedIn Import CLI application.
/// </summary>
internal static class MainMenu
{
    internal enum MenuOption
    {
        Settings = 1,
        DedupCsvs = 2,
        LinkedInImport = 3,
        Exit = 4
    }

    /// <summary>
    /// Displays the main menu banner with ASCII art.
    /// </summary>
    internal static void ShowBanner()
    {
        AnsiConsole.Clear();
        
        AnsiConsole.Write(
            new FigletText("LinkedIn Import")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[dim]Import your LinkedIn data export ZIP archives into a SQL Server database.[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays the main menu and returns the user's selection.
    /// </summary>
    internal static MenuOption Show(bool hasSettings)
    {
        ShowBanner();

        // Show current settings status
        if (hasSettings)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Settings configured");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] Settings not configured - please configure before importing");
        }
        AnsiConsole.WriteLine();

        var choices = new List<string>
        {
            "1. Settings - Configure ZIP directory and database connection",
        };

        // Only enable these options if settings are configured
        if (hasSettings)
        {
            choices.Add("2. Dedup CSVs - Deduplicate CSV files from ZIP folders");
            choices.Add("3. LinkedIn Import - Import LinkedIn data to database");
        }
        else
        {
            choices.Add("[dim]2. Dedup CSVs - Deduplicate CSV files from ZIP folders (configure settings first)[/]");
            choices.Add("[dim]3. LinkedIn Import - Import LinkedIn data to database (configure settings first)[/]");
        }

        choices.Add("4. Exit");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold cyan]What would you like to do?[/]")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                .AddChoices(choices));

        // Parse selection
        if (selection.StartsWith("1."))
            return MenuOption.Settings;
        if (selection.StartsWith("2.") && hasSettings)
            return MenuOption.DedupCsvs;
        if (selection.StartsWith("3.") && hasSettings)
            return MenuOption.LinkedInImport;
        if (selection.StartsWith("4."))
            return MenuOption.Exit;

        // If they selected a disabled option, show settings menu
        return MenuOption.Settings;
    }

    /// <summary>
    /// Shows a completion message and waits for user acknowledgment.
    /// </summary>
    internal static void ShowCompletionMessage(string message, bool success = true)
    {
        AnsiConsole.WriteLine();
        
        if (success)
        {
            AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(message)}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(message)}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to return to main menu...[/]");
        Console.ReadKey(true);
    }
}

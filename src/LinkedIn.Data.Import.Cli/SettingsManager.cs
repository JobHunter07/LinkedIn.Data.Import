using LinkedIn.Data.Import.Shared;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Manages application settings (ZIP directory and connection string).
/// </summary>
internal sealed class SettingsManager
{
    private readonly IConfiguration _config;
    private ImportOptions? _cachedSettings;

    public SettingsManager(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    /// Checks if settings are configured and valid.
    /// </summary>
    public bool HasValidSettings()
    {
        var zipDir = GetZipDirectory();
        var connectionString = GetConnectionString();

        return !string.IsNullOrWhiteSpace(zipDir) &&
               Directory.Exists(zipDir) &&
               !string.IsNullOrWhiteSpace(connectionString);
    }

    /// <summary>
    /// Gets the current settings, prompting the user to configure them if missing.
    /// </summary>
    public async Task<ImportOptions?> GetOrConfigureSettingsAsync(bool forceReconfigure = false)
    {
        if (!forceReconfigure && _cachedSettings is not null && HasValidSettings())
        {
            return _cachedSettings;
        }

        MainMenu.ShowBanner();
        
        AnsiConsole.Write(
            new Rule("[bold blue]Settings Configuration[/]")
                .RuleStyle("blue")
                .LeftJustified());
        AnsiConsole.WriteLine();

        var options = await ImportWizard.AskAsync(_config);
        
        if (options is not null)
        {
            _cachedSettings = options;
        }

        return options;
    }

    /// <summary>
    /// Gets current settings without prompting (returns null if not configured).
    /// </summary>
    public ImportOptions? GetCurrentSettings()
    {
        if (_cachedSettings is not null)
        {
            return _cachedSettings;
        }

        var zipDir = GetZipDirectory();
        var connectionString = GetConnectionString();

        if (string.IsNullOrWhiteSpace(zipDir) || string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        _cachedSettings = new ImportOptions
        {
            ZipRootDirectory = zipDir,
            ConnectionString = connectionString
        };

        return _cachedSettings;
    }

    /// <summary>
    /// Displays current settings in a formatted table.
    /// </summary>
    public void DisplayCurrentSettings()
    {
        AnsiConsole.WriteLine();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Setting[/]").Width(20))
            .AddColumn(new TableColumn("[bold]Value[/]"));

        var zipDir = GetZipDirectory() ?? "[red](not set)[/]";
        var connectionString = GetConnectionString();
        var maskedConnection = MaskConnectionString(connectionString);

        table.AddRow("ZIP Directory", Markup.Escape(zipDir));
        table.AddRow("Connection String", maskedConnection);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (HasValidSettings())
        {
            AnsiConsole.MarkupLine("[green]✓ Settings are configured and valid[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Settings are incomplete or invalid[/]");
        }
    }

    /// <summary>
    /// Clears cached settings (forces re-reading from config).
    /// </summary>
    public void ClearCache()
    {
        _cachedSettings = null;
    }

    private string? GetZipDirectory()
    {
        return ImportDefaultsProvider.GetDefaultZipRootDirectory(_config);
    }

    private string? GetConnectionString()
    {
        return ImportDefaultsProvider.GetDefaultConnectionString(_config);
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "[red](not set)[/]";
        }

        // Mask password in connection string
        var masked = connectionString;
        
        if (connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            var parts = connectionString.Split(';');
            masked = string.Join(";", parts.Select(part =>
            {
                if (part.Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                {
                    return "Password=********";
                }
                return part;
            }));
        }

        return Markup.Escape(masked);
    }
}

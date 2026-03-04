using System.Text.Json;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Manages updating appsettings.json with user preferences.
/// </summary>
internal static class AppsettingsManager
{
    private static readonly string AppsettingsPath = 
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>
    /// Updates the Import:ZipRootDirectory value in appsettings.json.
    /// </summary>
    public static void UpdateZipRootDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Read existing file or create new structure
        var settings = File.Exists(AppsettingsPath)
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(AppsettingsPath))
            : new Dictionary<string, JsonElement>();

        if (settings is null)
            settings = new Dictionary<string, JsonElement>();

        // Update the Import section
        var importSection = new Dictionary<string, object?>
        {
            ["ZipRootDirectory"] = path
        };

        // Preserve existing Import settings if present
        if (settings.TryGetValue("Import", out var existingImport))
        {
            var existingDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                existingImport.GetRawText());
            
            if (existingDict is not null)
            {
                foreach (var kvp in existingDict)
                {
                    if (kvp.Key != "ZipRootDirectory")
                    {
                        importSection[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        settings["Import"] = JsonSerializer.SerializeToElement(importSection);

        // Write back with pretty formatting
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(AppsettingsPath, JsonSerializer.Serialize(settings, options));
    }
}

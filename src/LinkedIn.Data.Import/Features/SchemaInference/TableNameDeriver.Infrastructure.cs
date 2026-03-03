using System.Text;
using System.Text.RegularExpressions;

namespace LinkedIn.Data.Import.Features.SchemaInference;

/// <summary>
/// Derives the target database table name from a CSV file name.
/// Converts to snake_case, strips the <c>.csv</c> extension, and sanitises to
/// alphanumeric characters and underscores only.
/// </summary>
public sealed partial class TableNameDeriver
{
    /// <summary>
    /// Derives a snake_case table name from <paramref name="csvFileName"/>.
    /// </summary>
    /// <param name="csvFileName">The CSV file name (with or without path).</param>
    /// <returns>A sanitised, snake_case table name.</returns>
    public string Derive(string csvFileName)
    {
        // Strip directory and extension.
        var baseName = Path.GetFileNameWithoutExtension(csvFileName);

        // Replace spaces and hyphens with underscores.
        baseName = baseName.Replace(' ', '_').Replace('-', '_');

        // Insert underscore before uppercase letters that follow lowercase letters
        // or digits (CamelCase → camel_case).
        baseName = CamelCaseRegex().Replace(baseName, "$1_$2");

        // Convert to lower-case.
        baseName = baseName.ToLowerInvariant();

        // Strip any characters that are not alphanumeric or underscore.
        baseName = NonAlphanumericRegex().Replace(baseName, string.Empty);

        // Collapse consecutive underscores.
        baseName = MultiUnderscoreRegex().Replace(baseName, "_");

        // Trim leading/trailing underscores.
        return baseName.Trim('_');
    }

    [GeneratedRegex(@"([a-z0-9])([A-Z])")]
    private static partial Regex CamelCaseRegex();

    [GeneratedRegex(@"[^a-z0-9_]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"_{2,}")]
    private static partial Regex MultiUnderscoreRegex();
}

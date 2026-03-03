namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Configuration supplied by the caller when starting an import run.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>
    /// Absolute path to the directory that contains the LinkedIn ZIP export
    /// archives (<c>Basic_LinkedInDataExport*.zip</c> /
    /// <c>Complete_LinkedInDataExport*.zip</c>).
    /// </summary>
    public required string ZipRootDirectory { get; init; }

    /// <summary>
    /// ADO.NET-compatible connection string for the target database where
    /// inferred tables and import-log records will be written.
    /// </summary>
    public required string ConnectionString { get; init; }
}

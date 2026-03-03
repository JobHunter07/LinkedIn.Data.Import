namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Captures the outcome of importing a single CSV file from a LinkedIn export.
/// </summary>
public sealed class FileImportResult
{
    /// <summary>The CSV file path (relative to the archive root).</summary>
    public required string SourceFile { get; init; }

    /// <summary>Whether this file was imported without errors.</summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>Number of net-new rows inserted from this file.</summary>
    public int InsertedCount { get; set; }

    /// <summary>Number of rows skipped because they were already in the import log.</summary>
    public int SkippedCount { get; set; }

    /// <summary>Errors that occurred while processing this file.</summary>
    public List<ImportError> Errors { get; } = [];
}

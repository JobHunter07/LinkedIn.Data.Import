namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Top-level result returned by <see cref="ILinkedInImporter.ImportAsync"/>.
/// Aggregates the per-file outcomes and overall counts for the run.
/// </summary>
public sealed class ImportResult
{
    /// <summary>
    /// <see langword="true"/> when every CSV file was imported without errors.
    /// <see langword="false"/> when one or more files encountered an error, or when
    /// a fatal pre-flight check (e.g. missing root directory) failed.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>Total rows inserted across all files in this run.</summary>
    public int TotalInserted { get; set; }

    /// <summary>Total rows skipped (already present in the import log) across all files.</summary>
    public int TotalSkipped { get; set; }

    /// <summary>All errors that occurred during this run.</summary>
    public List<ImportError> Errors { get; } = [];

    /// <summary>Per-file import results.</summary>
    public List<FileImportResult> FileResults { get; } = [];
}

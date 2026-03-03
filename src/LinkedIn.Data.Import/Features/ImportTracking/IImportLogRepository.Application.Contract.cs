namespace LinkedIn.Data.Import.Features.ImportTracking;

/// <summary>
/// Reads and writes import-log records for deduplication tracking.
/// </summary>
public interface IImportLogRepository
{
    /// <summary>
    /// Loads the set of row hashes already recorded for <paramref name="sourceFile"/>.
    /// Fetches only hashes for the given file to keep memory usage bounded.
    /// </summary>
    Task<HashSet<string>> LoadHashSetAsync(
        System.Data.IDbConnection connection,
        string sourceFile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an import-log entry within the caller's open
    /// <paramref name="transaction"/>. Must be called inside the same transaction
    /// as the row insert.
    /// </summary>
    Task RecordAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string sourceFile,
        string rowHash,
        DateTimeOffset importedAt,
        CancellationToken cancellationToken = default);
}

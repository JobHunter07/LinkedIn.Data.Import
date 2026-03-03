using Dapper;

namespace LinkedIn.Data.Import.Features.ImportTracking;

/// <summary>
/// Dapper-backed implementation of <see cref="IImportLogRepository"/>.
/// </summary>
public sealed class ImportLogRepository : IImportLogRepository
{
    /// <inheritdoc/>
    public async Task<HashSet<string>> LoadHashSetAsync(
        System.Data.IDbConnection connection,
        string sourceFile,
        CancellationToken cancellationToken = default)
    {
        // Fetch ONLY hashes for this source file to keep memory usage bounded.
        var hashes = await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT row_hash FROM import_log WHERE source_file = @sourceFile",
                new { sourceFile },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return new HashSet<string>(hashes, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task RecordAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string sourceFile,
        string rowHash,
        DateTimeOffset importedAt,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO import_log (source_file, row_hash, imported_at) VALUES (@sourceFile, @rowHash, @importedAt)",
                new { sourceFile, rowHash, importedAt },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

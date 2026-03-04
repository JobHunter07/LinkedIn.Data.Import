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

    /// <inheritdoc/>
    public async Task<ImportLogEntry?> GetByHashAsync(
        System.Data.IDbConnection connection,
        string sourceFile,
        string rowHash,
        CancellationToken cancellationToken = default)
    {
        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition(
                """
                SELECT id, source_file, row_hash, imported_at 
                FROM import_log 
                WHERE source_file = @sourceFile AND row_hash = @rowHash
                """,
                new { sourceFile, rowHash },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
            return null;

        // Handle potential DateTimeOffset parsing issues across different databases
        DateTimeOffset importedAt;
        if (row.imported_at is DateTimeOffset dto)
            importedAt = dto;
        else if (row.imported_at is string str)
            importedAt = DateTimeOffset.Parse(str);
        else
            importedAt = (DateTimeOffset)row.imported_at;

        return new ImportLogEntry
        {
            Id = row.id,
            SourceFile = row.source_file,
            RowHash = row.row_hash,
            ImportedAt = importedAt
        };
    }
}

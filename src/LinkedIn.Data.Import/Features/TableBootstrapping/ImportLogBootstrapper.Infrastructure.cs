using Dapper;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Creates the <c>import_log</c> table on the target database if it does not
/// already exist.
/// </summary>
public sealed class ImportLogBootstrapper : IImportLogBootstrapper
{
    private readonly ISqlDialect _dialect;

    /// <summary>Initialises the bootstrapper with the target SQL dialect.</summary>
    public ImportLogBootstrapper(ISqlDialect dialect) => _dialect = dialect;

    /// <inheritdoc/>
    public async Task EnsureCreatedAsync(
        System.Data.IDbConnection connection,
        CancellationToken cancellationToken = default)
    {
        var q = _dialect.QuoteIdentifier;

        var columnsDdl = string.Join(", ",
            $"{q("id")} INTEGER PRIMARY KEY AUTOINCREMENT",
            $"{q("source_file")} NVARCHAR(1000) NOT NULL",
            $"{q("row_hash")} NVARCHAR(64) NOT NULL",
            $"{q("imported_at")} DATETIMEOFFSET NOT NULL");

        var ddl = _dialect.CreateTableIfNotExists("import_log", columnsDdl);
        await connection.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}

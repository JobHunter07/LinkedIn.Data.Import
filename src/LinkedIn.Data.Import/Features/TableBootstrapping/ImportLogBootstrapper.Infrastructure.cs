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

        // Render the ID column in a dialect-appropriate way. SQLite uses
        // "INTEGER PRIMARY KEY AUTOINCREMENT" while SQL Server uses
        // "INT IDENTITY(1,1) PRIMARY KEY". Fall back to a plain BIGINT
        // primary key for unknown dialects.
        var idColumnDdl = _dialect switch
        {
            SqliteDialect => $"{q("id")} INTEGER PRIMARY KEY AUTOINCREMENT",
            SqlServerDialect => $"{q("id")} INT IDENTITY(1,1) PRIMARY KEY",
            _ => $"{q("id")} BIGINT PRIMARY KEY",
        };

        var columnsDdl = string.Join(", ",
            idColumnDdl,
            $"{q("source_file")} NVARCHAR(1000) NOT NULL",
            $"{q("row_hash")} NVARCHAR(64) NOT NULL",
            $"{q("imported_at")} DATETIMEOFFSET NOT NULL");

        var ddl = _dialect.CreateTableIfNotExists("import_log", columnsDdl);
        await connection.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}

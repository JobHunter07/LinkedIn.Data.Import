using Dapper;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// <see cref="ISqlDialect"/> implementation for SQLite databases.
/// Uses <c>pragma_table_info</c> for column discovery, double-quoted
/// identifiers per the SQLite standard, and maps SQL Server types to their
/// SQLite equivalents (e.g. <c>NVARCHAR(MAX)</c> → <c>TEXT</c>).
/// </summary>
public sealed class SqliteDialect : ISqlDialect
{
    /// <inheritdoc/>
    public string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <inheritdoc/>
    public string CreateTableIfNotExists(string tableName, string columnsDdl) =>
        $"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)} ({columnsDdl})";

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetExistingColumnNamesAsync(
        System.Data.IDbConnection connection,
        string tableName)
    {
        // pragma_table_info is a virtual table available in SQLite 3.16+;
        // it supports parameterised queries and returns a plain result set.
        var names = await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info(@table)",
            new { table = tableName }).ConfigureAwait(false);

        return names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// SQLite does not support the <c>NULL</c> column-constraint keyword.
    /// Nullable columns (the default) require no constraint. NOT NULL columns
    /// receive a <c>DEFAULT ''</c> clause so that <c>ALTER TABLE ADD COLUMN</c>
    /// succeeds even when the table already contains rows.
    /// </remarks>
    public string AddColumn(string tableName, string quotedColumnName, string sqlType, bool isNullable)
    {
        var normalised = NormalizeSqlType(sqlType);
        return isNullable
            ? $"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {quotedColumnName} {normalised}"
            : $"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {quotedColumnName} {normalised} NOT NULL DEFAULT ''";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Maps SQL Server canonical type names to their SQLite equivalents:
    /// <c>NVARCHAR(MAX)</c> → <c>TEXT</c>, <c>DATETIMEOFFSET</c> → <c>TEXT</c>,
    /// <c>DECIMAL(18,6)</c> → <c>REAL</c>, <c>BIGINT</c>/<c>INT</c>/<c>BIT</c> → <c>INTEGER</c>.
    /// </remarks>
    public string NormalizeSqlType(string sqlType) => sqlType.ToUpperInvariant() switch
    {
        "NVARCHAR(MAX)" => "TEXT",
        "DATETIMEOFFSET" => "TEXT",
        "DECIMAL(18,6)" => "REAL",
        "BIGINT" => "INTEGER",
        "INT" => "INTEGER",
        "BIT" => "INTEGER",
        _ => sqlType,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// SQLite does not recognise <c>NULL</c> as a column constraint keyword,
    /// so nullable columns return an empty string (nullable is the default).
    /// Non-nullable columns return <c>NOT NULL</c>.
    /// </remarks>
    public string GetNullabilityConstraint(bool isNullable) =>
        isNullable ? string.Empty : "NOT NULL";
}

using Dapper;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// <see cref="ISqlDialect"/> implementation for Microsoft SQL Server.
/// Uses <c>INFORMATION_SCHEMA.COLUMNS</c> for column discovery and square-bracket
/// quoting.
/// </summary>
public sealed class SqlServerDialect : ISqlDialect
{
    /// <inheritdoc/>
    public string QuoteIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]")}]";

    /// <inheritdoc/>
    public string CreateTableIfNotExists(string tableName, string columnsDdl)
    {
        var quoted = QuoteIdentifier(tableName);
        return $"""
            IF OBJECT_ID(N'{tableName.Replace("'", "''")}', N'U') IS NULL
            BEGIN
                CREATE TABLE {quoted} ({columnsDdl})
            END
            """;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetExistingColumnNamesAsync(
        System.Data.IDbConnection connection,
        string tableName)
    {
        var rows = await connection.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table",
            new { table = tableName }).ConfigureAwait(false);

        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public string AddColumn(string tableName, string quotedColumnName, string sqlType, bool isNullable)
    {
        var normalised = NormalizeSqlType(sqlType);
        var nullability = GetNullabilityConstraint(isNullable);
        return $"ALTER TABLE {QuoteIdentifier(tableName)} ADD {quotedColumnName} {normalised} {nullability}";
    }

    /// <inheritdoc/>
    /// <remarks>SQL Server types are used as-is; no translation needed.</remarks>
    public string NormalizeSqlType(string sqlType) => sqlType;

    /// <inheritdoc/>
    public string GetNullabilityConstraint(bool isNullable) => isNullable ? "NULL" : "NOT NULL";
}

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Database-specific SQL generation for DDL statements and schema queries.
/// Implement for SQL Server, SQLite, PostgreSQL, etc.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Returns a <c>CREATE TABLE</c> statement that is a no-op when the table
    /// already exists.
    /// </summary>
    string CreateTableIfNotExists(string tableName, string columnsDdl);

    /// <summary>
    /// Returns the column names that already exist in <paramref name="tableName"/>.
    /// </summary>
    Task<IReadOnlySet<string>> GetExistingColumnNamesAsync(
        System.Data.IDbConnection connection,
        string tableName);

    /// <summary>
    /// Returns an <c>ALTER TABLE … ADD COLUMN</c> statement.
    /// </summary>
    string AddColumn(string tableName, string quotedColumnName, string sqlType, bool isNullable);

    /// <summary>Quotes a database identifier to prevent SQL injection.</summary>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// Translates a canonical SQL type string (e.g. <c>NVARCHAR(MAX)</c>) to the
    /// dialect-appropriate equivalent (e.g. <c>TEXT</c> for SQLite).
    /// </summary>
    string NormalizeSqlType(string sqlType);

    /// <summary>
    /// Returns the DDL nullability fragment for a column (e.g. empty string,
    /// <c>NULL</c>, or <c>NOT NULL</c>), suitable for embedding in CREATE TABLE
    /// or ALTER TABLE statements.
    /// </summary>
    string GetNullabilityConstraint(bool isNullable);
}

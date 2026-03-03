namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Describes a single column inferred from a CSV file sample.
/// </summary>
/// <param name="Name">The sanitised column name as it will appear in the database.</param>
/// <param name="SqlType">The SQL data-type string (e.g. <c>INT</c>, <c>NVARCHAR(MAX)</c>).</param>
/// <param name="IsNullable">Whether all sampled values were empty, making the column nullable.</param>
/// <param name="ClrType">The .NET CLR type that corresponds to the SQL type.</param>
public sealed record ColumnDefinition(
    string Name,
    string SqlType,
    bool IsNullable,
    Type ClrType);

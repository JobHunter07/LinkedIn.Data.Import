namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// The result of schema inference for a single CSV file — the table name and
/// the ordered list of column definitions ready for DDL generation.
/// </summary>
/// <param name="TableName">The derived database table name (snake_case, sanitised).</param>
/// <param name="Columns">Ordered list of inferred column definitions.</param>
public sealed record InferredSchema(
    string TableName,
    IReadOnlyList<ColumnDefinition> Columns);

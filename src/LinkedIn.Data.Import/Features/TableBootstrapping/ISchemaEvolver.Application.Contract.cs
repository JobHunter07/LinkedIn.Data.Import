using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Adds columns from the current CSV schema that are absent from the existing table.
/// Never drops or renames columns.
/// </summary>
public interface ISchemaEvolver
{
    /// <summary>
    /// Compares the columns in <paramref name="schema"/> against the existing
    /// columns in <paramref name="tableName"/> and emits
    /// <c>ALTER TABLE … ADD COLUMN</c> statements for any missing columns.
    /// </summary>
    Task<Result> EvolveAsync(
        System.Data.IDbConnection connection,
        string tableName,
        InferredSchema schema,
        CancellationToken cancellationToken = default);
}

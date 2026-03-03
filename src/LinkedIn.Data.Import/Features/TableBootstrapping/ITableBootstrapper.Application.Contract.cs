using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Creates a database table from an inferred schema when it does not yet exist.
/// </summary>
public interface ITableBootstrapper
{
    /// <summary>
    /// Ensures the table described by <paramref name="schema"/> exists, creating
    /// it if necessary. Publishes <see cref="TableReadyEvent"/> when done.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the table was newly created; <see langword="false"/>
    /// if it already existed. Returns <see cref="ErrorCode.TableCreationFailure"/>
    /// on DDL error.
    /// </returns>
    Task<Result<bool>> EnsureTableAsync(
        System.Data.IDbConnection connection,
        InferredSchema schema,
        CancellationToken cancellationToken = default);
}

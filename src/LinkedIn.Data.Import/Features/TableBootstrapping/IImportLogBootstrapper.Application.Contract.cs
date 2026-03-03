namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Ensures the <c>import_log</c> tracking table exists in the target database.
/// </summary>
public interface IImportLogBootstrapper
{
    /// <summary>
    /// Creates the <c>import_log</c> table if it does not already exist.
    /// </summary>
    Task EnsureCreatedAsync(
        System.Data.IDbConnection connection,
        CancellationToken cancellationToken = default);
}

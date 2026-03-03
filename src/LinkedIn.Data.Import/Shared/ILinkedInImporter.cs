namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Entry point for the LinkedIn data import library.
/// </summary>
/// <remarks>
/// <para>
/// Implementations discover and extract LinkedIn ZIP exports, infer their
/// schemas, create/evolve database tables, and insert only net-new rows
/// (idempotent across multiple runs).
/// </para>
/// <para>
/// The method never throws for any <em>known</em> failure condition; all
/// foreseeable errors are captured inside the returned <see cref="ImportResult"/>.
/// The only legitimate throw is <see cref="ArgumentNullException"/> when
/// <paramref name="options"/> is <see langword="null"/>.
/// </para>
/// </remarks>
public interface ILinkedInImporter
{
    /// <summary>
    /// Runs a full import from the ZIP archives in
    /// <see cref="ImportOptions.ZipRootDirectory"/> into the database identified
    /// by <see cref="ImportOptions.ConnectionString"/>.
    /// </summary>
    /// <param name="options">Import configuration — must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="ImportResult"/> summarising the run; check
    /// <see cref="ImportResult.IsSuccess"/> before using the counts.
    /// </returns>
    Task<ImportResult> ImportAsync(
        ImportOptions options,
        CancellationToken cancellationToken = default);
}

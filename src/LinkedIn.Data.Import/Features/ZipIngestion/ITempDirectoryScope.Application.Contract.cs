namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Controls the lifetime of a temporary working directory.
/// Guaranteed to delete the directory on <see cref="IDisposable.Dispose"/>,
/// whether processing succeeds or fails.
/// </summary>
public interface ITempDirectoryScope : IDisposable
{
    /// <summary>Absolute path of the temporary directory.</summary>
    string DirectoryPath { get; }
}

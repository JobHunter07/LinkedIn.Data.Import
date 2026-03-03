namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Creates and owns a temporary directory, deleting it when disposed.
/// Guarantees cleanup on both success and failure paths.
/// </summary>
public sealed class TempDirectoryScope : ITempDirectoryScope
{
    private bool _disposed;

    /// <summary>
    /// Initialises a new scope, creating a unique subdirectory under
    /// <see cref="Path.GetTempPath"/>.
    /// </summary>
    public TempDirectoryScope()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"linkedin-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <inheritdoc/>
    public string DirectoryPath { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup — swallow IO errors on dispose.
        }
    }
}

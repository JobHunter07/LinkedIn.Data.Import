using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Extracts a single ZIP archive into a caller-managed directory.
/// </summary>
public interface IZipExtractor
{
    /// <summary>
    /// Extracts the contents of <paramref name="archive"/> into a unique
    /// subdirectory under <paramref name="extractionRoot"/>.
    /// </summary>
    /// <returns>
    /// The absolute paths of all files extracted, or
    /// <see cref="ErrorCode.ArchiveCorrupt"/> on failure.
    /// </returns>
    Result<IReadOnlyList<string>> Extract(
        DiscoveredArchive archive,
        string extractionRoot);
}


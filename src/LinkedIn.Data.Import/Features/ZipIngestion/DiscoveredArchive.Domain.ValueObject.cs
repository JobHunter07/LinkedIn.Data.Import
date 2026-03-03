using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Represents a discovered LinkedIn export archive on the file system.
/// </summary>
/// <param name="FilePath">Absolute path to the ZIP archive.</param>
/// <param name="ArchiveType">Whether the archive is Basic or Complete.</param>
public sealed record DiscoveredArchive(
    string FilePath,
    ArchiveType ArchiveType);

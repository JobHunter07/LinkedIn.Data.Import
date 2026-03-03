using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Discovers LinkedIn export ZIP archives in a root directory.
/// </summary>
public interface IZipDiscovery
{
    /// <summary>
    /// Scans <paramref name="rootDirectory"/> for LinkedIn export ZIPs.
    /// </summary>
    /// <returns>
    /// <para>
    /// A <see cref="DiscoveryResult"/> on success — even when only one archive
    /// type is present (<see cref="DiscoveryResult.HasBothTypes"/> will be
    /// <see langword="false"/> in that case and the caller should record a
    /// <see cref="ErrorCode.SingleArchiveTypeOnly"/> warning).
    /// </para>
    /// <para>
    /// Returns <see cref="ErrorCode.RootDirectoryNotFound"/> when the directory
    /// does not exist, or <see cref="ErrorCode.NoArchivesFound"/> when no matching
    /// archives are present.
    /// </para>
    /// </returns>
    Result<DiscoveryResult> Discover(string rootDirectory);
}

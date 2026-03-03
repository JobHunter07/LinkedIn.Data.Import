using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// File-system-based implementation of <see cref="IZipDiscovery"/>.
/// Recognises <c>Basic_LinkedInDataExport*.zip</c> and
/// <c>Complete_LinkedInDataExport*.zip</c> (case-insensitive).
/// </summary>
public sealed class ZipDiscovery : IZipDiscovery
{
    /// <inheritdoc/>
    public Result<DiscoveryResult> Discover(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            return Result<DiscoveryResult>.Fail(
                ErrorCode.RootDirectoryNotFound,
                $"Root directory '{rootDirectory}' does not exist.");

        var all = Directory.GetFiles(rootDirectory, "*.zip", SearchOption.TopDirectoryOnly);

        var basic = all
            .Where(f => Path.GetFileName(f).StartsWith(
                "Basic_LinkedInDataExport", StringComparison.OrdinalIgnoreCase))
            .Select(f => new DiscoveredArchive(f, ArchiveType.Basic))
            .ToList();

        var complete = all
            .Where(f => Path.GetFileName(f).StartsWith(
                "Complete_LinkedInDataExport", StringComparison.OrdinalIgnoreCase))
            .Select(f => new DiscoveredArchive(f, ArchiveType.Complete))
            .ToList();

        var archives = (IReadOnlyList<DiscoveredArchive>)basic.Concat(complete).ToList();

        if (archives.Count == 0)
            return Result<DiscoveryResult>.Fail(
                ErrorCode.NoArchivesFound,
                $"No LinkedIn export archives found in '{rootDirectory}'.");

        var hasBothTypes = basic.Count > 0 && complete.Count > 0;
        return Result<DiscoveryResult>.Ok(new DiscoveryResult(archives, hasBothTypes));
    }
}

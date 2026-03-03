using System.IO.Compression;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Extracts a discovered ZIP archive into a unique subdirectory under a
/// caller-managed extraction root using <see cref="ZipFile"/>.
/// </summary>
public sealed class ZipExtractor : IZipExtractor
{
    /// <inheritdoc/>
    public Result<IReadOnlyList<string>> Extract(
        DiscoveredArchive archive,
        string extractionRoot)
    {
        try
        {
            // Each archive gets its own subdirectory so their contents don't clash.
            var destDir = Path.Combine(
                extractionRoot,
                Path.GetFileNameWithoutExtension(archive.FilePath));
            Directory.CreateDirectory(destDir);

            ZipFile.ExtractToDirectory(archive.FilePath, destDir, overwriteFiles: true);

            var extracted = Directory
                .GetFiles(destDir, "*", SearchOption.AllDirectories)
                .ToList();

            return Result<IReadOnlyList<string>>.Ok(extracted);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return Result<IReadOnlyList<string>>.Fail(
                ErrorCode.ArchiveCorrupt,
                $"Failed to extract '{archive.FilePath}': {ex.Message}");
        }
    }
}

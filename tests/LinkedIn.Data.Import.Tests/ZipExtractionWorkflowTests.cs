using LinkedIn.Data.Import.Features.ZipIngestion;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Tests for the extraction workflow that needs to happen before deduplication.
/// </summary>
public sealed class ZipExtractionWorkflowTests
{
    [Fact]
    public void ZipDiscovery_ShouldFindZipFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create test ZIP files with LinkedIn naming pattern
            var zip1 = Path.Combine(tempDir, "Basic_LinkedInDataExport_01-01-2026.zip");
            var zip2 = Path.Combine(tempDir, "Complete_LinkedInDataExport_02-01-2026.zip");
            File.WriteAllText(zip1, "dummy");
            File.WriteAllText(zip2, "dummy");

            var discovery = new ZipDiscovery();

            // Act
            var result = discovery.Discover(tempDir);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.Archives.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ZipDiscovery_ShouldFailWhenNoZips()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "test.txt"), "not a zip");

            var discovery = new ZipDiscovery();

            // Act
            var result = discovery.Discover(tempDir);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCode.NoArchivesFound, result.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

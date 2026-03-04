using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.ImportTracking;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Tests for CSV deduplication that safely removes duplicate rows
/// from CSV files without modifying originals.
/// </summary>
public sealed class CsvDeduplicatorTests
{
    [Fact]
    public async Task DeduplicateAsync_ShouldRemoveDuplicateRows()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "test.csv");
            var csvContent = """
                Name,Age,Email
                John,30,john@example.com
                Jane,25,jane@example.com
                John,30,john@example.com
                Bob,35,bob@example.com
                Jane,25,jane@example.com
                """;
            await File.WriteAllTextAsync(csvPath, csvContent);

            var hasher = new RowHasher();
            var deduplicator = new CsvDeduplicator(hasher);

            // Act
            var result = await deduplicator.DeduplicateAsync(csvPath, tempDir);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.TotalRows); // Excluding header
            Assert.Equal(3, result.UniqueRows);
            Assert.Equal(2, result.DuplicatesRemoved);
            Assert.True(File.Exists(result.DeduplicatedFilePath));

            // Verify original is unchanged
            var originalContent = await File.ReadAllTextAsync(csvPath);
            Assert.Equal(csvContent, originalContent);

            // Verify deduplicated file has correct content
            var deduplicatedLines = await File.ReadAllLinesAsync(result.DeduplicatedFilePath);
            Assert.Equal(4, deduplicatedLines.Length); // Header + 3 unique rows
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task DeduplicateAsync_ShouldHandleNoDuplicates()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "unique.csv");
            var csvContent = """
                Name,Age
                Alice,20
                Bob,30
                Charlie,40
                """;
            await File.WriteAllTextAsync(csvPath, csvContent);

            var hasher = new RowHasher();
            var deduplicator = new CsvDeduplicator(hasher);

            // Act
            var result = await deduplicator.DeduplicateAsync(csvPath, tempDir);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.TotalRows);
            Assert.Equal(3, result.UniqueRows);
            Assert.Equal(0, result.DuplicatesRemoved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task DeduplicateAsync_ShouldHandleEmptyFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "empty.csv");
            await File.WriteAllTextAsync(csvPath, "Name,Age\n");

            var hasher = new RowHasher();
            var deduplicator = new CsvDeduplicator(hasher);

            // Act
            var result = await deduplicator.DeduplicateAsync(csvPath, tempDir);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.UniqueRows);
            Assert.Equal(0, result.DuplicatesRemoved);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task DeduplicateAsync_ShouldPreserveFirstOccurrence()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "order.csv");
            var csvContent = """
                ID,Name
                1,First
                2,Second
                1,First
                3,Third
                2,Second
                """;
            await File.WriteAllTextAsync(csvPath, csvContent);

            var hasher = new RowHasher();
            var deduplicator = new CsvDeduplicator(hasher);

            // Act
            var result = await deduplicator.DeduplicateAsync(csvPath, tempDir);

            // Assert
            var lines = await File.ReadAllLinesAsync(result.DeduplicatedFilePath);
            Assert.Equal("ID,Name", lines[0]);
            Assert.Equal("1,First", lines[1]);
            Assert.Equal("2,Second", lines[2]);
            Assert.Equal("3,Third", lines[3]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

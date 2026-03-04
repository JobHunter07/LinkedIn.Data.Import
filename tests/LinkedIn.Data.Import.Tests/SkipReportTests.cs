using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.Sqlite;

namespace LinkedIn.Data.Import.Tests;

public sealed class SkipReportTests
{
    [Fact]
    public void FileImportResult_ShouldCollectSkippedSamples()
    {
        // Arrange
        var result = new FileImportResult { SourceFile = "test.csv" };
        var sample = new SkippedRecordSample
        {
            Hash = "abc123",
            ColumnNames = ["Name", "Age"],
            FieldValues = ["John", "30"]
        };

        // Act
        result.SkippedSamples.Add(sample);

        // Assert
        Assert.Single(result.SkippedSamples);
        Assert.Equal("abc123", result.SkippedSamples[0].Hash);
        Assert.Equal(2, result.SkippedSamples[0].ColumnNames.Length);
        Assert.Equal("John", result.SkippedSamples[0].FieldValues[0]);
    }

    [Fact]
    public void SkippedRecordSample_ShouldStoreColumnNamesAndValues()
    {
        // Arrange & Act
        var sample = new SkippedRecordSample
        {
            Hash = "def456",
            ColumnNames = ["FirstName", "LastName", "Email"],
            FieldValues = ["Jane", "Doe", "jane@example.com"]
        };

        // Assert
        Assert.Equal(3, sample.ColumnNames.Length);
        Assert.Equal(3, sample.FieldValues.Length);
        Assert.Equal("FirstName", sample.ColumnNames[0]);
        Assert.Equal("jane@example.com", sample.FieldValues[2]);
    }

    [Fact]
    public async Task ImportLogRepository_GetByHashAsync_ShouldReturnNullWhenNotFound()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var dialect = new SqliteDialect();
        var importLogBootstrapper = new ImportLogBootstrapper(dialect);
        await importLogBootstrapper.EnsureCreatedAsync(connection);

        var repo = new ImportLogRepository();

        // Act
        var result = await repo.GetByHashAsync(
            connection,
            "nonexistent.csv",
            "nohash",
            CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ImportLogRepository_GetByHashAsync_ShouldReturnExistingEntry()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var dialect = new SqliteDialect();
        var importLogBootstrapper = new ImportLogBootstrapper(dialect);
        await importLogBootstrapper.EnsureCreatedAsync(connection);

        var repo = new ImportLogRepository();
        var sourceFile = "test.csv";
        var hash = "abc123def456";
        var importedAt = DateTimeOffset.UtcNow;

        using var transaction = connection.BeginTransaction();
        await repo.RecordAsync(connection, transaction, sourceFile, hash, importedAt);
        transaction.Commit();

        // Act
        var result = await repo.GetByHashAsync(connection, sourceFile, hash);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sourceFile, result.SourceFile);
        Assert.Equal(hash, result.RowHash);
        Assert.True(result.ImportedAt > DateTimeOffset.MinValue);
    }
}

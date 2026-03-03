using Dapper;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Features.ZipIngestion;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.Sqlite;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// End-to-end integration tests using real ZIP archives and an in-memory SQLite
/// database (task 7.5).
/// </summary>
public sealed class EndToEndIntegrationTests : IAsyncDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"linkin-e2e-{Guid.NewGuid():N}");

    public EndToEndIntegrationTests() => Directory.CreateDirectory(_tempDir);

    public async ValueTask DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        await Task.CompletedTask;
    }

    private LinkedInImporter BuildImporter(SqliteConnection conn)
    {
        var dialect = new SqliteDialect();
        var events = new InProcessEventDispatcher();
        var evolver = new SchemaEvolver(dialect);
        var tableBootstrapper = new TableBootstrapper(dialect, evolver, events);
        var inferrer = new CsvSchemaInferrer(new TypeDetector(), new TableNameDeriver(), events);
        var importLog = new ImportLogRepository();
        var hasher = new RowHasher();
        var logBootstrapper = new ImportLogBootstrapper(dialect);
        var csvImporter = new CsvFileImporter(inferrer, tableBootstrapper, importLog, hasher, dialect, events);

        // Register cross-feature event handler: schema inferred → bootstrap table.
        events.Register<CsvSchemaInferredEvent>(async (evt, ct) =>
            await tableBootstrapper.EnsureTableAsync(conn, evt.Schema, ct).ConfigureAwait(false));

        var discovery = new ZipDiscovery();
        var extractor = new ZipExtractor();
        var ingestUseCase = new IngestZipsUseCase(discovery, extractor, events);

        return new LinkedInImporter(
            ingestUseCase, csvImporter, tableBootstrapper, logBootstrapper,
            events, () => conn);
    }

    private string WriteBasicArchive(string csvContent)
    {
        var zipPath = Path.Combine(_tempDir, "Basic_LinkedInDataExport_test.zip");
        TestHelpers.WriteZipArchive(zipPath, new Dictionary<string, string>
        {
            ["connections.csv"] = csvContent,
        });
        return _tempDir;
    }

    [Fact]
    public async Task FirstImport_AllRowsInserted()
    {
        var rootDir = WriteBasicArchive(
            "FirstName,LastName\nAlice,Smith\nBob,Jones");

        using var conn = TestHelpers.CreateConnection();
        var importer = BuildImporter(conn);
        var options = new ImportOptions
        {
            ZipRootDirectory = rootDir,
            ConnectionString = "Data Source=:memory:",
        };

        var result = await importer.ImportAsync(options);

        Assert.True(result.IsSuccess || result.Errors.All(e => e.Code == ErrorCode.SingleArchiveTypeOnly));
        Assert.True(result.TotalInserted >= 2);
    }

    [Fact]
    public async Task ReImport_IsIdempotent_ZeroRowsInsertedSecondTime()
    {
        var rootDir = WriteBasicArchive(
            "FirstName,LastName\nAlice,Smith\nBob,Jones");

        using var conn = TestHelpers.CreateConnection();

        // First run.
        var importer1 = BuildImporter(conn);
        var options = new ImportOptions
        {
            ZipRootDirectory = rootDir,
            ConnectionString = "Data Source=:memory:",
        };
        await importer1.ImportAsync(options);

        // Second run with same data.
        var importer2 = BuildImporter(conn);
        var result2 = await importer2.ImportAsync(options);

        Assert.Equal(0, result2.TotalInserted);
        Assert.Equal(2, result2.TotalSkipped);
    }

    [Fact]
    public async Task PartialNewData_OnlyNewRowsInserted()
    {
        var rootDir = WriteBasicArchive(
            "FirstName,LastName\nAlice,Smith\nBob,Jones");

        using var conn = TestHelpers.CreateConnection();
        var options = new ImportOptions
        {
            ZipRootDirectory = rootDir,
            ConnectionString = "Data Source=:memory:",
        };

        // First run — 2 rows.
        var importer1 = BuildImporter(conn);
        await importer1.ImportAsync(options);

        // Overwrite archive with 3 rows (original 2 + 1 new).
        TestHelpers.WriteZipArchive(
            Path.Combine(rootDir, "Basic_LinkedInDataExport_test.zip"),
            new Dictionary<string, string>
            {
                ["connections.csv"] = "FirstName,LastName\nAlice,Smith\nBob,Jones\nCarol,Green",
            });

        var importer2 = BuildImporter(conn);
        var result2 = await importer2.ImportAsync(options);

        Assert.Equal(1, result2.TotalInserted);
        Assert.Equal(2, result2.TotalSkipped);
    }

    [Fact]
    public async Task MissingRootDirectory_ReturnsError()
    {
        using var conn = TestHelpers.CreateConnection();
        var importer = BuildImporter(conn);
        var options = new ImportOptions
        {
            ZipRootDirectory = Path.Combine(_tempDir, "nonexistent"),
            ConnectionString = "Data Source=:memory:",
        };

        var result = await importer.ImportAsync(options);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ErrorCode.RootDirectoryNotFound);
    }
}

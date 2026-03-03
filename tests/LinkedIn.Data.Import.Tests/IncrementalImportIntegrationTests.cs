using System.Threading.Channels;
using Dapper;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.Sqlite;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Integration tests for incremental import idempotency (tasks 6.6, 6.7).
/// </summary>
public sealed class IncrementalImportIntegrationTests : IAsyncDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"linkin-test-{Guid.NewGuid():N}");
    private readonly SqliteConnection _conn = TestHelpers.CreateConnection();
    private readonly ISqlDialect _dialect = TestHelpers.Dialect();
    private readonly InProcessEventDispatcher _events = new();
    private readonly CsvFileImporter _importer;

    public IncrementalImportIntegrationTests()
    {
        Directory.CreateDirectory(_tempDir);

        var evolver = new SchemaEvolver(_dialect);
        var bootstrapper = new TableBootstrapper(_dialect, evolver, _events);
        var inferrer = new CsvSchemaInferrer(new TypeDetector(), new TableNameDeriver(), _events);
        var importLog = new ImportLogRepository();
        var hasher = new RowHasher();

        // Wire CsvSchemaInferredEvent → TableBootstrapper so the table is
        // created/evolved before row insertion.
        _events.Register<CsvSchemaInferredEvent>(async (evt, ct) =>
            await bootstrapper.EnsureTableAsync(_conn, evt.Schema, ct).ConfigureAwait(false));

        _importer = new CsvFileImporter(inferrer, bootstrapper, importLog, hasher, _dialect, _events);
    }

    public async ValueTask DisposeAsync()
    {
        _conn.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<int> RunImportForFile(string csvPath)
    {
        await TestHelpers.EnsureImportLogAsync(_conn);

        var channel = Channel.CreateUnbounded<CsvProcessingJob>();
        await channel.Writer.WriteAsync(new CsvProcessingJob(csvPath, "test.zip", ArchiveType.Basic));
        channel.Writer.Complete();

        int completedFiles = 0;
        _events.Register<FileImportCompletedEvent>((_, _) => { completedFiles++; return Task.CompletedTask; });

        await _importer.RunAsync(_conn, channel.Reader);
        return completedFiles;
    }

    [Fact]
    public async Task ImportSameFileTwice_SecondRunInsertsZeroRows()
    {
        var csvPath = Path.Combine(_tempDir, "test_data.csv");
        TestHelpers.WriteCsvFile(csvPath,
            headers: ["FirstName", "LastName"],
            rows: [["Alice", "Smith"], ["Bob", "Jones"]]);

        // First import.
        await RunImportForFile(csvPath);

        var rows1 = await TestHelpers.GetTableRowsAsync(_conn, "test_data");
        Assert.Equal(2, rows1.Count);

        // Second import — same file, same data.
        var channel2 = Channel.CreateUnbounded<CsvProcessingJob>();
        await channel2.Writer.WriteAsync(new CsvProcessingJob(csvPath, "test.zip", ArchiveType.Basic));
        channel2.Writer.Complete();

        FileImportResult? result2 = null;
        _events.Register<FileImportCompletedEvent>((evt, _) => { result2 = evt.Result; return Task.CompletedTask; });

        await _importer.RunAsync(_conn, channel2.Reader);

        Assert.NotNull(result2);
        Assert.Equal(0, result2!.InsertedCount);
        Assert.Equal(2, result2.SkippedCount);

        // DB should still have exactly 2 rows.
        var rows2 = await TestHelpers.GetTableRowsAsync(_conn, "test_data");
        Assert.Equal(2, rows2.Count);
    }

    [Fact]
    public async Task ImportFileAddNewRowsReimport_OnlyNewRowsInserted()
    {
        var csvPath = Path.Combine(_tempDir, "test_grow.csv");

        // Initial data: 2 rows.
        TestHelpers.WriteCsvFile(csvPath,
            headers: ["Name", "Score"],
            rows: [["Alice", "10"], ["Bob", "20"]]);

        await RunImportForFile(csvPath);

        var rows1 = await TestHelpers.GetTableRowsAsync(_conn, "test_grow");
        Assert.Equal(2, rows1.Count);

        // Add one new row to the file.
        TestHelpers.WriteCsvFile(csvPath,
            headers: ["Name", "Score"],
            rows: [["Alice", "10"], ["Bob", "20"], ["Carol", "30"]]);

        var channel2 = Channel.CreateUnbounded<CsvProcessingJob>();
        await channel2.Writer.WriteAsync(new CsvProcessingJob(csvPath, "test.zip", ArchiveType.Basic));
        channel2.Writer.Complete();

        FileImportResult? result2 = null;
        _events.Register<FileImportCompletedEvent>((evt, _) => { result2 = evt.Result; return Task.CompletedTask; });

        await _importer.RunAsync(_conn, channel2.Reader);

        Assert.NotNull(result2);
        Assert.Equal(1, result2!.InsertedCount);
        Assert.Equal(2, result2.SkippedCount);

        var rows2 = await TestHelpers.GetTableRowsAsync(_conn, "test_grow");
        Assert.Equal(3, rows2.Count);
    }
}

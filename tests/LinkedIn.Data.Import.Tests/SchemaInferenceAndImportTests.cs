using System.Globalization;
using System.Threading.Channels;
using Dapper;
using Microsoft.Data.Sqlite;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Xunit;

namespace LinkedIn.Data.Import.Tests;

public class SchemaInferenceAndImportTests
{
    [Fact]
    public async Task CsvSchemaInferrer_UniqueColumnNames_WhenHeadersCollide()
    {
        var csv = "Company Names,Company-Names,Company_Names\nAcme,Acme,Acme\n";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, csv);

            var typeDetector = new TypeDetector();
            var tableNameDeriver = new TableNameDeriver();
            var events = new InProcessEventDispatcher();
            var inferrer = new CsvSchemaInferrer(typeDetector, tableNameDeriver, events);

            var result = await inferrer.InferAsync(path);
            Assert.True(result.IsSuccess, result.ErrorMessage);

            var names = result.Value.Columns.Select(c => c.Name).ToList();
            Assert.Equal(3, names.Count);
            Assert.Equal(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), names.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TypeDetector_SetsNullable_WhenAnyEmptySample()
    {
        var detector = new TypeDetector();
        detector.Infer(new[] { "1", "" }, out var sqlType, out var clrType, out var isNullable);

        Assert.True(isNullable);
        // Should still infer numeric type when non-empty values indicate so
        Assert.Equal("INT", sqlType);
        Assert.Equal(typeof(int), clrType);
    }

    [Fact]
    public async Task CsvFileImporter_ParsesDateAndInsertsRow_IntoSqlite()
    {
        // Prepare CSV with a DATETIMEOFFSET-like value and an empty nullable column
        var csv = "when,notes\n2023-01-02T12:34:56Z,\n";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, csv);

            await using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            var dialect = new SqliteDialect();

            // Ensure import_log exists
            var importLogBootstrapper = new ImportLogBootstrapper(dialect);
            await importLogBootstrapper.EnsureCreatedAsync(connection);

            var typeDetector = new TypeDetector();
            var tableNameDeriver = new TableNameDeriver();
            var events = new InProcessEventDispatcher();
            var inferrer = new CsvSchemaInferrer(typeDetector, tableNameDeriver, events);

            var evolver = new SchemaEvolver(dialect);
            var bootstrapper = new TableBootstrapper(dialect, evolver, events);
            var importLog = new ImportLogRepository();
            var hasher = new RowHasher();

            var importer = new CsvFileImporter(inferrer, bootstrapper, importLog, hasher, dialect, events);

            var channel = Channel.CreateUnbounded<CsvProcessingJob>();
            await channel.Writer.WriteAsync(new CsvProcessingJob(path, "test.zip", ArchiveType.Basic));
            channel.Writer.Complete();

            // Run importer which should create the table and insert the row
            await importer.RunAsync(connection, channel.Reader);

            var tableName = tableNameDeriver.Derive(path);
            var count = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM \"{tableName}\"");
            Assert.Equal(1, count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

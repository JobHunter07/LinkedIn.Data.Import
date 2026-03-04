using System.Data;
using System.Text;
using System.Threading.Channels;
using Dapper;
using Microsoft.Data.SqlClient;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Xunit;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Tests for SQL Server NULL value handling during schema inference and import.
/// These tests verify that columns allow NULLs even when NULLs appear beyond
/// the sample size or in later data rows.
/// </summary>
public class SqlServerNullHandlingTests
{
    private const string ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=LinkedIn.Data.Import;Integrated Security=true;";

    [Fact]
    public async Task SchemaInference_CreatesNullableColumns_ForAllTextColumns()
    {
        // Create CSV with all non-empty values in sample (200 rows)
        // but the schema should still allow NULLs since later rows might have them
        var csv = new StringBuilder();
        csv.AppendLine("Message,Link,FirstName");
        
        // Add 200 rows with values (within sample size)
        for (int i = 1; i <= 200; i++)
        {
            csv.AppendLine($"Message{i},http://link{i}.com,Name{i}");
        }
        
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, csv.ToString());

            var typeDetector = new TypeDetector();
            var tableNameDeriver = new TableNameDeriver();
            var events = new InProcessEventDispatcher();
            var inferrer = new CsvSchemaInferrer(typeDetector, tableNameDeriver, events);

            var result = await inferrer.InferAsync(path);
            Assert.True(result.IsSuccess, result.ErrorMessage);

            // All columns should be nullable for text columns since we can't guarantee
            // that all rows in the entire file (beyond sample) have values
            var messageCol = result.Value.Columns.First(c => c.Name == "Message");
            var linkCol = result.Value.Columns.First(c => c.Name == "Link");
            var firstNameCol = result.Value.Columns.First(c => c.Name == "FirstName");

            Assert.True(messageCol.IsNullable, "Message column should be nullable");
            Assert.True(linkCol.IsNullable, "Link column should be nullable");
            Assert.True(firstNameCol.IsNullable, "FirstName column should be nullable");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportToSqlServer_HandlesNullValues_InInvitationsTable()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var tableName = "test_invitations_" + Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            // Create CSV matching Invitations.csv structure with some NULL values
            var csv = new StringBuilder();
            csv.AppendLine("First Name,Last Name,Message,Direction");
            csv.AppendLine("John,Doe,,INCOMING"); // NULL Message
            csv.AppendLine("Jane,Smith,Hello!,OUTGOING");
            csv.AppendLine("Bob,Jones,,INCOMING"); // NULL Message

            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, csv.ToString());

                var dialect = new SqlServerDialect();
                
                // Setup import infrastructure
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

                // Override the derived table name for cleanup
                var actualTableName = tableNameDeriver.Derive(path);
                
                var channel = Channel.CreateUnbounded<CsvProcessingJob>();
                await channel.Writer.WriteAsync(new CsvProcessingJob(path, "test.zip", ArchiveType.Basic));
                channel.Writer.Complete();

                // Run importer - this should NOT throw NULL constraint violations
                await importer.RunAsync(connection, channel.Reader);

                // Verify all rows were inserted
                var count = await connection.QuerySingleAsync<int>(
                    $"SELECT COUNT(*) FROM {dialect.QuoteIdentifier(actualTableName)}");
                Assert.Equal(3, count);

                // Verify NULL values were inserted correctly
                var rows = await connection.QueryAsync<dynamic>(
                    $"SELECT First_Name, Last_Name, Message FROM {dialect.QuoteIdentifier(actualTableName)} ORDER BY First_Name");
                var rowList = rows.ToList();
                
                Assert.Equal("Bob", rowList[0].First_Name);
                Assert.Null(rowList[0].Message); // Should be NULL
                
                Assert.Equal("Jane", rowList[1].First_Name);
                Assert.Equal("Hello!", rowList[1].Message);
                
                Assert.Equal("John", rowList[2].First_Name);
                Assert.Null(rowList[2].Message); // Should be NULL

                // Cleanup
                await connection.ExecuteAsync($"DROP TABLE {dialect.QuoteIdentifier(actualTableName)}");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        finally
        {
            // Additional cleanup in case of test failure
            await connection.ExecuteAsync($"IF OBJECT_ID(N'{tableName}', N'U') IS NOT NULL DROP TABLE {tableName}");
        }
    }

    [Fact]
    public async Task ImportToSqlServer_HandlesNullValues_InLearningTable()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Course Title,Content_Saved,Completed_Date");
            csv.AppendLine("C# Fundamentals,,2024-01-01"); // NULL Content_Saved
            csv.AppendLine("Azure Basics,Yes,2024-01-15");
            csv.AppendLine("SQL Server,,2024-02-01"); // NULL Content_Saved

            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, csv.ToString());

                var dialect = new SqlServerDialect();
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

                var actualTableName = tableNameDeriver.Derive(path);
                
                var channel = Channel.CreateUnbounded<CsvProcessingJob>();
                await channel.Writer.WriteAsync(new CsvProcessingJob(path, "test.zip", ArchiveType.Basic));
                channel.Writer.Complete();

                await importer.RunAsync(connection, channel.Reader);

                var count = await connection.QuerySingleAsync<int>(
                    $"SELECT COUNT(*) FROM {dialect.QuoteIdentifier(actualTableName)}");
                Assert.Equal(3, count);

                // Cleanup
                await connection.ExecuteAsync($"DROP TABLE {dialect.QuoteIdentifier(actualTableName)}");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        finally
        {
        }
    }

    [Fact]
    public async Task ImportToSqlServer_HandlesNullValues_InJobApplicationsTable()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Company,Job_Title,Question_And_Answers,Applied_Date");
            csv.AppendLine("Acme Corp,Software Engineer,,2024-01-01"); // NULL Question_And_Answers
            csv.AppendLine("Tech Inc,DevOps Engineer,Q: Why? A: Because,2024-01-15");
            csv.AppendLine("Data LLC,Data Analyst,,2024-02-01"); // NULL Question_And_Answers

            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, csv.ToString());

                var dialect = new SqlServerDialect();
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

                var actualTableName = tableNameDeriver.Derive(path);
                
                var channel = Channel.CreateUnbounded<CsvProcessingJob>();
                await channel.Writer.WriteAsync(new CsvProcessingJob(path, "test.zip", ArchiveType.Basic));
                channel.Writer.Complete();

                await importer.RunAsync(connection, channel.Reader);

                var count = await connection.QuerySingleAsync<int>(
                    $"SELECT COUNT(*) FROM {dialect.QuoteIdentifier(actualTableName)}");
                Assert.Equal(3, count);

                // Cleanup
                await connection.ExecuteAsync($"DROP TABLE {dialect.QuoteIdentifier(actualTableName)}");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        finally
        {
        }
    }

    [Fact]
    public async Task ImportToSqlServer_HandlesNullValues_InSavedJobsTable()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Company,Job_Title,Saved_Date");
            csv.AppendLine("Acme Corp,,2024-01-01"); // NULL Job_Title
            csv.AppendLine("Tech Inc,Senior Developer,2024-01-15");
            csv.AppendLine("Data LLC,,2024-02-01"); // NULL Job_Title

            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, csv.ToString());

                var dialect = new SqlServerDialect();
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

                var actualTableName = tableNameDeriver.Derive(path);
                
                var channel = Channel.CreateUnbounded<CsvProcessingJob>();
                await channel.Writer.WriteAsync(new CsvProcessingJob(path, "test.zip", ArchiveType.Basic));
                channel.Writer.Complete();

                await importer.RunAsync(connection, channel.Reader);

                var count = await connection.QuerySingleAsync<int>(
                    $"SELECT COUNT(*) FROM {dialect.QuoteIdentifier(actualTableName)}");
                Assert.Equal(3, count);

                // Cleanup
                await connection.ExecuteAsync($"DROP TABLE {dialect.QuoteIdentifier(actualTableName)}");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        finally
        {
        }
    }
}

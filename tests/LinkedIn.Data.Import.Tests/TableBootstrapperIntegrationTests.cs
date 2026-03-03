using Dapper;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.Sqlite;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Integration tests for <see cref="TableBootstrapper"/> using an in-memory
/// SQLite database (task 4.6).
/// </summary>
public sealed class TableBootstrapperIntegrationTests : IDisposable
{
    private readonly SqliteConnection _conn = TestHelpers.CreateConnection();
    private readonly ISqlDialect _dialect = TestHelpers.Dialect();
    private readonly InProcessEventDispatcher _events = new();
    private readonly TableBootstrapper _sut;

    public TableBootstrapperIntegrationTests()
    {
        var evolver = new SchemaEvolver(_dialect);
        _sut = new TableBootstrapper(_dialect, evolver, _events);
    }

    public void Dispose() => _conn.Dispose();

    [Fact]
    public async Task EnsureTableAsync_FirstRun_CreatesTable()
    {
        var schema = new InferredSchema("contacts", [
            new ColumnDefinition("name", "NVARCHAR(MAX)", false, typeof(string)),
            new ColumnDefinition("email", "NVARCHAR(MAX)", true, typeof(string)),
        ]);

        var result = await _sut.EnsureTableAsync(_conn, schema);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value); // isNewlyCreated

        // Verify table exists and has expected columns.
        var cols = await _conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('contacts')");
        var colList = cols.ToHashSet();
        Assert.Contains("name", colList);
        Assert.Contains("email", colList);
        Assert.Contains("id", colList);
        Assert.Contains("created_at", colList);
    }

    [Fact]
    public async Task EnsureTableAsync_SecondRun_IsNoOp()
    {
        var schema = new InferredSchema("contacts2", [
            new ColumnDefinition("name", "NVARCHAR(MAX)", false, typeof(string)),
        ]);

        // First run creates the table.
        var r1 = await _sut.EnsureTableAsync(_conn, schema);
        Assert.True(r1.Value); // newly created

        // Second run should be a no-op.
        var r2 = await _sut.EnsureTableAsync(_conn, schema);
        Assert.True(r2.IsSuccess);
        Assert.False(r2.Value); // not newly created
    }

    [Fact]
    public async Task EnsureTableAsync_TableReadyEventPublished()
    {
        TableReadyEvent? received = null;
        _events.Register<TableReadyEvent>((evt, _) => { received = evt; return Task.CompletedTask; });

        var schema = new InferredSchema("contacts3", [
            new ColumnDefinition("title", "NVARCHAR(MAX)", false, typeof(string)),
        ]);

        await _sut.EnsureTableAsync(_conn, schema);

        Assert.NotNull(received);
        Assert.Equal("contacts3", received.TableName);
    }

    [Fact]
    public async Task SchemaEvolver_AddsNewColumn_WhenTableAlreadyExists()
    {
        // Create table with one column.
        var schemaV1 = new InferredSchema("contacts4", [
            new ColumnDefinition("name", "NVARCHAR(MAX)", false, typeof(string)),
        ]);
        await _sut.EnsureTableAsync(_conn, schemaV1);

        // Re-run with schema that has an extra column.
        var schemaV2 = new InferredSchema("contacts4", [
            new ColumnDefinition("name", "NVARCHAR(MAX)", false, typeof(string)),
            new ColumnDefinition("phone", "NVARCHAR(MAX)", true, typeof(string)),
        ]);
        var result = await _sut.EnsureTableAsync(_conn, schemaV2);

        Assert.True(result.IsSuccess);

        var cols = await _conn.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('contacts4')");
        Assert.Contains("phone", cols);
    }
}

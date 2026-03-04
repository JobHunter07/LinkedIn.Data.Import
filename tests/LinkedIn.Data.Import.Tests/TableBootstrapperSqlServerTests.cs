using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Xunit;

namespace LinkedIn.Data.Import.Tests;

public class TableBootstrapperSqlServerTests
{
    private const string ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=LinkedIn.Data.Import;Integrated Security=true;";

    [Fact]
    public async Task EnsureTableAsync_CreatesTable_OnSqlServerLocalDb()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Ensure clean state
        await connection.ExecuteAsync("IF OBJECT_ID(N'dbo.test_table', N'U') IS NOT NULL DROP TABLE dbo.test_table;");

        var dialect = new SqlServerDialect();
        var evolver = new SchemaEvolver(dialect);
        var events = new InProcessEventDispatcher();
        var sut = new TableBootstrapper(dialect, evolver, events);

        var schema = new InferredSchema("test_table", new List<ColumnDefinition>
        {
            new ColumnDefinition("name", "NVARCHAR(MAX)", false, typeof(string)),
            new ColumnDefinition("email", "NVARCHAR(MAX)", true, typeof(string)),
        });

        var result = await sut.EnsureTableAsync(connection, schema);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);

        var rows = await connection.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table",
            new { table = "test_table" });

        var cols = rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("id", cols);
        Assert.Contains("name", cols);
        Assert.Contains("email", cols);
        Assert.Contains("created_at", cols);
    }
}

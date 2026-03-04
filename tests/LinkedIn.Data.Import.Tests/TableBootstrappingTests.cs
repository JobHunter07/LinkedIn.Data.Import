using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using Xunit;

namespace LinkedIn.Data.Import.Tests;

public class TableBootstrappingTests
{
    private const string ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=LinkedIn.Data.Import;Integrated Security=true;";

    [Fact]
    public async Task EnsureCreated_CreatesTable_WhenNotExists_SqlServer()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Ensure clean state
        await connection.ExecuteAsync("IF OBJECT_ID(N'dbo.import_log', N'U') IS NOT NULL DROP TABLE dbo.import_log;");

        var dialect = new SqlServerDialect();
        var bootstrapper = new ImportLogBootstrapper(dialect);

        await bootstrapper.EnsureCreatedAsync(connection);

        var rows = await connection.QueryAsync<(string COLUMN_NAME, string DATA_TYPE)>(
            "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table",
            new { table = "import_log" });

        var columns = rows.Select(r => r.COLUMN_NAME).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("id", columns);
        Assert.Contains("source_file", columns);
        Assert.Contains("row_hash", columns);
        Assert.Contains("imported_at", columns);

        var idRow = rows.First(r => string.Equals(r.COLUMN_NAME, "id", StringComparison.OrdinalIgnoreCase));
        Assert.True(idRow.DATA_TYPE == "int" || idRow.DATA_TYPE == "bigint");
    }
}

using System.IO.Compression;
using Dapper;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.Sqlite;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Shared utilities for integration tests using an in-memory SQLite database.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Creates and opens an in-memory SQLite connection.
    /// Caller is responsible for disposal.
    /// </summary>
    public static SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return conn;
    }

    /// <summary>Creates a <see cref="SqliteDialect"/> instance.</summary>
    public static SqliteDialect Dialect() => new();

    /// <summary>
    /// Bootstraps the import_log table using the given connection.
    /// </summary>
    public static async Task EnsureImportLogAsync(SqliteConnection conn)
    {
        var bootstrapper = new ImportLogBootstrapper(Dialect());
        await bootstrapper.EnsureCreatedAsync(conn);
    }

    /// <summary>
    /// Creates a minimal CSV file at the given path with the supplied headers/rows.
    /// </summary>
    public static void WriteCsvFile(string path, string[] headers, string[][] rows)
    {
        var lines = new List<string> { string.Join(",", headers) };
        lines.AddRange(rows.Select(r => string.Join(",", r)));
        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// Creates a ZIP archive at <paramref name="zipPath"/> containing
    /// <paramref name="files"/> (relative name → content).
    /// </summary>
    public static void WriteZipArchive(string zipPath, Dictionary<string, string> files)
    {
        using var stream = File.Create(zipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in files)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }

    /// <summary>Returns the rows present in a table as a list of dictionaries.</summary>
    public static async Task<List<IDictionary<string, object>>> GetTableRowsAsync(
        SqliteConnection conn, string tableName)
    {
        var results = await conn.QueryAsync($"SELECT * FROM \"{tableName}\"");
        return results.Cast<IDictionary<string, object>>().ToList();
    }
}

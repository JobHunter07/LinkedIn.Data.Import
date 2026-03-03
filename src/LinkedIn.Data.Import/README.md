# LinkedIn.Data.Import

A .NET 10 class library for importing LinkedIn data exports (ZIP archives) into a SQL database incrementally and idempotently.

## Overview

LinkedIn data exports arrive as two ZIP archives:

| Archive | Pattern |
|---|---|
| Basic  | `Basic_LinkedInDataExport*.zip`  |
| Complete | `Complete_LinkedInDataExport*.zip` |

This library discovers those archives in a directory, infers a relational schema from each CSV they contain, bootstraps the target database tables (including schema evolution), and imports only rows that have not previously been loaded.

---

## Quick Start

### 1. Register the library

```csharp
// Program.cs / Startup
services.AddLinkedInImporter(
    connectionFactory: () => new SqlConnection(connectionString));
```

By default `SqlServerDialect` is used for production. Pass a custom `dialectFactory` to target a different database (e.g., `SqliteDialect` for testing).

### 2. Run an import

```csharp
var importer = serviceProvider.GetRequiredService<ILinkedInImporter>();

var result = await importer.ImportAsync(new ImportOptions
{
    ZipRootDirectory = @"C:\LinkedInExports",
    ConnectionString  = "Server=...;Database=...;",
});

Console.WriteLine($"Inserted: {result.TotalInserted}, Skipped: {result.TotalSkipped}");

if (!result.IsSuccess)
{
    foreach (var err in result.Errors)
        Console.Error.WriteLine($"[{err.Code}] {err.SourceFile}: {err.Message}");
}
```

---

## `ImportOptions`

| Property | Type | Description |
|---|---|---|
| `ZipRootDirectory` | `string` | Absolute path to the folder containing the LinkedIn ZIP archives. |
| `ConnectionString` | `string` | ADO.NET connection string for the target database (informational; the connection itself is provided via the factory delegate registered at DI startup). |

---

## `ImportResult`

| Member | Type | Description |
|---|---|---|
| `IsSuccess` | `bool` | `true` when every CSV processed without errors. |
| `TotalInserted` | `int` | Total new rows inserted across all files. |
| `TotalSkipped` | `int` | Total rows skipped because they matched an existing import-log entry. |
| `Errors` | `List<ImportError>` | All errors encountered (pre-flight + per-file). |
| `FileResults` | `List<FileImportResult>` | One entry per processed CSV file. |

Non-fatal warnings (e.g., `ErrorCode.SingleArchiveTypeOnly` — only one archive type present) appear in `Errors` but do not halt the import.

---

## Idempotency

Re-running the import against the same ZIP archives is always safe:

- Each row is fingerprinted with a **SHA-256 hash** of its pipe-delimited, trimmed column values.
- The `import_log` table records `(source_file, row_hash, imported_at)` for every successfully inserted row.
- On subsequent runs, rows whose hash already exists in `import_log` are **skipped**, not re-inserted.

This means:

```
Run 1: 2 rows in connections.csv  → TotalInserted = 2, TotalSkipped = 0
Run 2: same file, same data       → TotalInserted = 0, TotalSkipped = 2
Run 3: file gains 1 new row       → TotalInserted = 1, TotalSkipped = 2
```

---

## Schema Inference & Evolution

On first import of a CSV:

1. Up to 200 sample rows are read to infer column types (priority order: `INT` → `BIGINT` → `DECIMAL` → `DATETIMEOFFSET` → `BIT` → `NVARCHAR(MAX)`).
2. The table is created with an auto-increment `id` PK and a `created_at` timestamp, plus all inferred columns.

On subsequent imports of the same CSV with additional columns:

- **New columns are added** via `ALTER TABLE … ADD COLUMN`.
- **No columns are ever dropped or renamed** — only additive schema evolution.

---

## Error Codes

| Code | Meaning |
|---|---|
| `RootDirectoryNotFound` | `ZipRootDirectory` does not exist. |
| `NoArchivesFound` | No matching ZIP archives found in the directory. |
| `SingleArchiveTypeOnly` | Only one archive type (Basic or Complete) was found; import continues. |
| `ArchiveCorrupt` | A ZIP file could not be opened or extracted. |
| `CsvParseFailure` | A CSV file could not be read (encoding, format error, etc.). |
| `SchemaInferenceFailure` | A CSV has no headers or contains no data rows. |
| `TableCreationFailure` | DDL execution failed (permissions, naming conflict, etc.). |
| `RowInsertFailure` | An insert or import-log write failed; the file's transaction is rolled back. |

---

## Architecture

```
LinkedInImporter (orchestrator)
├── ZipIngestion           producer — discovers ZIPs, extracts CSVs, writes jobs to bounded channel
│   ├── ZipDiscovery
│   ├── ZipExtractor
│   └── IngestZipsUseCase
├── SchemaInference        infers SQL schema from CSV headers/rows
│   ├── CsvSchemaInferrer
│   ├── TypeDetector
│   └── TableNameDeriver
├── TableBootstrapping     creates/evolves target tables
│   ├── TableBootstrapper
│   ├── SchemaEvolver
│   └── ImportLogBootstrapper
├── ImportTracking         SHA-256 deduplication via import_log
│   ├── RowHasher
│   └── ImportLogRepository
└── IncrementalImport      consumer — reads channel, inserts new rows per-file
    └── CsvFileImporter
```

Domain events wire features together without direct coupling:

```
CsvSchemaInferredEvent  → TableBootstrapper.EnsureTableAsync
TableReadyEvent         → (consumed externally if needed)
FileImportCompletedEvent → LinkedInImporter accumulates ImportResult
ImportSessionCompletedEvent → final summary
```

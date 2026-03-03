## 1. Project Setup

- [ ] 1.1 Create `LinkedIn.Data.Import` Class Library project (targeting .NET 8+)
- [ ] 1.2 Add NuGet dependencies: `CsvHelper`, `Dapper`, and the appropriate database driver (e.g., `Microsoft.Data.SqlClient` or `Npgsql`)
- [ ] 1.3 Define public-facing types: `ILinkedInImporter`, `ImportOptions`, `ImportResult`, `FileImportResult`, `ImportError`
- [ ] 1.4 Define `ImportConfigurationException` for invalid configuration errors
- [ ] 1.5 Set up internal namespace structure: `ZipIngestion`, `SchemaInference`, `TableBootstrapping`, `IncrementalImport`, `ImportTracking`

## 2. ZIP Ingestion

- [ ] 2.1 Implement `ZipDiscovery` — scan configured directory for `Basic_LinkedInDataExport*.zip` and `Complete_LinkedInDataExport*.zip` (case-insensitive)
- [ ] 2.2 Raise `ImportConfigurationException` when the root directory does not exist
- [ ] 2.3 Return a warning (not a failure) when only one archive type is found; return an error result when no archives are found
- [ ] 2.4 Implement `ZipExtractor` — extract each discovered archive to a unique temp subdirectory using `System.IO.Compression.ZipFile`
- [ ] 2.5 Wrap extraction in try/catch; log corrupt archives as `ImportError` entries and continue with remaining archives
- [ ] 2.6 Implement `ITempDirectoryScope` (IDisposable) to guarantee temp directory cleanup on success and failure

## 3. CSV Schema Inference

- [ ] 3.1 Implement `CsvSchemaInferrer` — open each extracted `.csv` with `CsvHelper`, read headers, and stream up to 200 data rows
- [ ] 3.2 Implement `TypeDetector` — apply priority-ordered type inference: `INT` → `BIGINT` → `DECIMAL` → `DATETIMEOFFSET` → `BIT` → `NVARCHAR(MAX)`
- [ ] 3.3 Mark columns as nullable when all sampled values are empty/whitespace; fall back to `NVARCHAR(MAX)` for those columns
- [ ] 3.4 Implement `TableNameDeriver` — convert CSV file name to snake_case, strip `.csv` extension, sanitize to alphanumeric + underscores
- [ ] 3.5 Produce an `InferredSchema` value type containing: table name, and a list of `ColumnDefinition` (name, SQL type, nullable, CLR type)
- [ ] 3.6 Write unit tests for `TypeDetector` covering all six type lanes and the empty-values edge case
- [ ] 3.7 Write unit tests for `TableNameDeriver` covering standard names, multi-word names, and special-character names

## 4. Table Bootstrapping

- [ ] 4.1 Implement `ImportLogBootstrapper` — emit `CREATE TABLE IF NOT EXISTS import_log (id, source_file, row_hash, imported_at)` on startup
- [ ] 4.2 Implement `TableBootstrapper` — emit `CREATE TABLE IF NOT EXISTS <table>` from `InferredSchema`; include auto-increment `id` PK and `created_at` column
- [ ] 4.3 Implement `SchemaEvolver` — query existing columns via `INFORMATION_SCHEMA.COLUMNS` (or equivalent); emit `ALTER TABLE ... ADD COLUMN` for each column present in the CSV but absent from the table
- [ ] 4.4 Ensure `SchemaEvolver` never emits DROP or RENAME statements
- [ ] 4.5 Sanitize all column and table identifiers (quoted identifiers) before embedding in DDL strings to prevent injection
- [ ] 4.6 Write integration tests for `TableBootstrapper`: first-run creates table; second-run is a no-op

## 5. Import Tracking

- [ ] 5.1 Implement `RowHasher` — concatenate trimmed column values (pipe-delimited, column order), compute SHA-256, return lowercase hex string
- [ ] 5.2 Implement `ImportLogRepository` with two operations: `LoadHashSetAsync(sourceFile)` and `RecordAsync(sourceFile, rowHash, importedAt)` (within caller's transaction)
- [ ] 5.3 Ensure `LoadHashSetAsync` fetches only hashes for the given `source_file` to bound memory usage
- [ ] 5.4 Write unit tests for `RowHasher` verifying stable output for identical inputs and distinct output for differing inputs

## 6. Incremental Import

- [ ] 6.1 Implement `CsvFileImporter` — orchestrates schema inference → table bootstrapping → hash-set load → row iteration → insert + log
- [ ] 6.2 Open a database transaction per CSV file; commit on success, roll back on any error (including `import_log` writes)
- [ ] 6.3 For each row: compute hash → check hash set → skip if present → insert row + log entry if new
- [ ] 6.4 Accumulate `InsertedCount`, `SkippedCount` per file; populate `FileImportResult`
- [ ] 6.5 Use `CsvHelper` streaming (do not load entire file into memory)
- [ ] 6.6 Write integration tests: import same file twice → second run inserts zero rows
- [ ] 6.7 Write integration test: import file, add new rows, re-import → only new rows inserted

## 7. Public API & Orchestration

- [ ] 7.1 Implement `LinkedInImporter : ILinkedInImporter` — top-level orchestrator calling `ZipDiscovery` → `ZipExtractor` → per-file `CsvFileImporter`
- [ ] 7.2 Wire up `CancellationToken` propagation through all async calls
- [ ] 7.3 Build and return `ImportResult` from per-file results: `Success`, `TotalInserted`, `TotalSkipped`, `Errors`, `FileResults`
- [ ] 7.4 Register `ILinkedInImporter` / `LinkedInImporter` for dependency injection (provide `ServiceCollectionExtensions.AddLinkedInImporter()`)
- [ ] 7.5 Write an end-to-end integration test using a real SQLite (or in-memory) database and sample ZIP archives covering: first import, re-import (idempotent), and partial-new-data import

## 8. Quality & Documentation

- [ ] 8.1 Add XML doc comments to all public types and members
- [ ] 8.2 Add a `README.md` to the library project documenting `ImportOptions` fields, quick-start usage, and idempotency guarantees
- [ ] 8.3 Ensure all tests pass in CI (no flaky state from shared database connections)
- [ ] 8.4 Review DDL sanitization and confirm no SQL injection vectors remain

## 1. Project Setup

- [ ] 1.1 Create `LinkedIn.Data.Import` Class Library project (targeting .NET 10)
- [ ] 1.2 Add NuGet dependencies: `CsvHelper`, `Dapper`, and the appropriate database driver (e.g., `Microsoft.Data.SqlClient` or `Npgsql`)
- [ ] 1.3 Define public-facing and shared types: `ILinkedInImporter`, `ImportOptions`, `ImportResult`, `FileImportResult`, `ImportError`, `CsvProcessingJob` (channel job record with `CsvFilePath`, `SourceArchiveName`, `ArchiveType`), `ArchiveType` enum (`Basic` / `Complete`); add `Result<T>` (generic) and `Result` (non-generic) response types with `IsSuccess`, `Value`, `ErrorCode`, and `ErrorMessage` members; add `ErrorCode` enum covering all known failure codes (`RootDirectoryNotFound`, `NoArchivesFound`, `SingleArchiveTypeOnly`, `ArchiveCorrupt`, `CsvParseFailure`, `SchemaInferenceFailure`, `DatabaseConnectionFailure`, `TableCreationFailure`, `RowInsertFailure`)
- [ ] 1.4 Establish error-handling rule: all known/foreseeable failures MUST return `Result.Fail(ErrorCode, message)` — never throw; reserve exceptions only for `ArgumentNullException` (null `ImportOptions` programmer error) and `OperationCanceledException` (cancellation token); document this rule in a code comment on `Result<T>`
- [ ] 1.5 Scaffold vertical-slice folder structure under `Features/`: one flat folder per feature (`ZipIngestion`, `SchemaInference`, `TableBootstrapping`, `ImportTracking`, `IncrementalImport`) with no sub-folders; encode layer and DDD type in each file name using the convention `{ClassName}.{Layer}.{Type}.cs` (e.g., `IZipDiscovery.Application.Contract.cs`, `DiscoveredArchive.Domain.ValueObject.cs`, `ZipDiscovery.Infrastructure.cs`); add flat `Shared/` folder for `ILinkedInImporter.cs`, `ImportOptions.cs`, `ImportResult.cs`, `FileImportResult.cs`, `ImportError.cs`, `Result.cs`, `ErrorCode.cs`, `IDomainEvent.cs`, `IEventDispatcher.cs`, all five domain event record files, and `CsvProcessingJob.cs`
- [ ] 1.6 Implement domain event infrastructure: define `IDomainEvent` marker interface; define `IEventDispatcher` contract with `PublishAsync<TEvent>(TEvent, CancellationToken)`; implement `InProcessEventDispatcher` (keyed handler registry, no reflection, no MediatR); register all event handlers at DI startup so features subscribe without knowing each other; write unit tests verifying a published event reaches its registered handler
- [ ] 1.7 Define the five domain event records in `Shared/`: `ArchiveExtractedEvent` (archive name, extracted CSV paths list), `CsvSchemaInferredEvent` (CSV file path, `InferredSchema`), `TableReadyEvent` (table name, `IsNewlyCreated` bool), `FileImportCompletedEvent` (source file, `FileImportResult`), `ImportSessionCompletedEvent` (`ImportResult`)
- [ ] 1.8 Set up `Channel<CsvProcessingJob>` in the orchestrator: create a `BoundedChannel<CsvProcessingJob>` with `FullMode = Wait`, `SingleWriter = true`, capacity capped at 128; expose the writer to `ZipIngestion` and the reader to the CSV processing pipeline via constructor injection

## 2. ZIP Ingestion

- [ ] 2.1 Implement `ZipDiscovery` — scan configured directory for `Basic_LinkedInDataExport*.zip` and `Complete_LinkedInDataExport*.zip` (case-insensitive); tag each result with `ArchiveType.Basic` or `ArchiveType.Complete`
- [ ] 2.2 Return `Result.Fail(ErrorCode.RootDirectoryNotFound, ...)` when the root directory does not exist — do NOT throw an exception
- [ ] 2.3 Return `Result.Fail(ErrorCode.SingleArchiveTypeOnly, ...)` (non-fatal warning) when only one archive type is found; return `Result.Fail(ErrorCode.NoArchivesFound, ...)` when no archives are found — neither condition throws
- [ ] 2.4 Implement `ZipExtractor` — extract each discovered archive to a unique temp subdirectory using `System.IO.Compression.ZipFile`
- [ ] 2.5 Wrap extraction in try/catch for I/O and ZIP format errors; capture as `Result.Fail(ErrorCode.ArchiveCorrupt, ...)` in `FileImportResult.Errors` and continue with remaining archives — do NOT rethrow or propagate the exception
- [ ] 2.6 Implement `ITempDirectoryScope` (IDisposable) to guarantee temp directory cleanup on success and failure
- [ ] 2.7 After each ZIP is successfully extracted, write one `CsvProcessingJob` per extracted CSV file to the `Channel<CsvProcessingJob>` writer; after all ZIPs are written, complete the channel writer; publish `ArchiveExtractedEvent` (containing archive name and extracted CSV path list) via `IEventDispatcher`

## 3. CSV Schema Inference

- [ ] 3.1 Implement `CsvSchemaInferrer` — open each extracted `.csv` with `CsvHelper`, read headers, and stream up to 200 data rows; on success publish `CsvSchemaInferredEvent(csvFilePath, inferredSchema)` via `IEventDispatcher`
- [ ] 3.2 Implement `TypeDetector` — apply priority-ordered type inference: `INT` → `BIGINT` → `DECIMAL` → `DATETIMEOFFSET` → `BIT` → `NVARCHAR(MAX)`
- [ ] 3.3 Mark columns as nullable when all sampled values are empty/whitespace; fall back to `NVARCHAR(MAX)` for those columns
- [ ] 3.4 Implement `TableNameDeriver` — convert CSV file name to snake_case, strip `.csv` extension, sanitize to alphanumeric + underscores
- [ ] 3.5 Produce an `InferredSchema` value type containing: table name, and a list of `ColumnDefinition` (name, SQL type, nullable, CLR type)
- [ ] 3.6 Write unit tests for `TypeDetector` covering all six type lanes and the empty-values edge case
- [ ] 3.7 Write unit tests for `TableNameDeriver` covering standard names, multi-word names, and special-character names

## 4. Table Bootstrapping

- [ ] 4.1 Implement `ImportLogBootstrapper` — emit `CREATE TABLE IF NOT EXISTS import_log (id, source_file, row_hash, imported_at)` on startup
- [ ] 4.2 Implement `TableBootstrapper` — subscribe to `CsvSchemaInferredEvent`; emit `CREATE TABLE IF NOT EXISTS <table>` from `InferredSchema`; include auto-increment `id` PK and `created_at` column; publish `TableReadyEvent(tableName, isNewlyCreated)` via `IEventDispatcher` after DDL completes
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

- [ ] 6.1 Implement `CsvFileImporter` — reads `CsvProcessingJob` items from `Channel<CsvProcessingJob>.Reader`; for each job orchestrates: schema inference (which publishes `CsvSchemaInferredEvent`) → await `TableReadyEvent` for that table → hash-set load → row iteration → insert + log
- [ ] 6.2 Open a database transaction per CSV file; on success commit and record `Result.Ok`; on any error roll back the transaction (including `import_log` writes) and capture `Result.Fail(ErrorCode.RowInsertFailure, ...)` in `FileImportResult` — do NOT throw; continue processing remaining files
- [ ] 6.3 For each row: compute hash → check hash set → skip if present → insert row + log entry if new
- [ ] 6.4 Accumulate `InsertedCount`, `SkippedCount` per file; populate `FileImportResult`
- [ ] 6.5 Use `CsvHelper` streaming (do not load entire file into memory)
- [ ] 6.6 Write integration tests: import same file twice → second run inserts zero rows
- [ ] 6.7 Write integration test: import file, add new rows, re-import → only new rows inserted
- [ ] 6.8 After each CSV file finishes (success or failure), publish `FileImportCompletedEvent(sourceFile, fileImportResult)` via `IEventDispatcher`; the orchestrator's handler appends the result to the running `ImportResult` aggregate

## 7. Public API & Orchestration

- [ ] 7.1 Implement `LinkedInImporter : ILinkedInImporter` — create `Channel<CsvProcessingJob>`; start ZIP ingestion (producer) and CSV processing pipeline (consumer) as concurrent tasks using `Task.WhenAll`; register a `FileImportCompletedEvent` handler to accumulate `FileImportResult` entries; await channel completion before building final `ImportResult`; publish `ImportSessionCompletedEvent` on finish
- [ ] 7.2 Wire up `CancellationToken` propagation through all async calls
- [ ] 7.3 Build and return `ImportResult` from `FileImportCompletedEvent` accumulations: `Success`, `TotalInserted`, `TotalSkipped`, `Errors`, `FileResults`; confirm `ImportSessionCompletedEvent` carries the final result
- [ ] 7.4 Register `ILinkedInImporter` / `LinkedInImporter` for dependency injection (provide `ServiceCollectionExtensions.AddLinkedInImporter()`)
- [ ] 7.5 Write an end-to-end integration test using a real SQLite (or in-memory) database and sample ZIP archives covering: first import, re-import (idempotent), and partial-new-data import

## 8. Quality & Documentation

- [ ] 8.1 Add XML doc comments to all public types and members
- [ ] 8.2 Add a `README.md` to the library project documenting `ImportOptions` fields, quick-start usage, and idempotency guarantees
- [ ] 8.3 Ensure all tests pass in CI (no flaky state from shared database connections)
- [ ] 8.4 Review DDL sanitization and confirm no SQL injection vectors remain

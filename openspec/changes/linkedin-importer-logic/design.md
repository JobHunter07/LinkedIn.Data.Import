## Context

LinkedIn users can request their data as ZIP exports in two tiers — **Basic** and **Complete**. There is currently no automated mechanism to ingest those exports into our database. All downstream features (analytics, job-tracking, profile enrichment) depend on this ingestion layer existing before any other work proceeds.

The system will be implemented as a standalone C# Class Library (`LinkedIn.Data.Import`) targeting **.NET 10** with no UI dependency. Consumers will trigger it programmatically when they detect the user has signaled readiness (e.g., placed ZIPs in the watched directory).

## Goals / Non-Goals

**Goals:**
- Read and extract `Basic_LinkedInDataExport*.zip` and `Complete_LinkedInDataExport*.zip` from a configured root directory.
- Infer SQL schema (table name, column names, data types) from CSV headers and sample rows.
- Create tables automatically on first run; skip creation if tables already exist.
- Insert only net-new rows on every subsequent run (idempotent, incremental).
- Record a fingerprint of every imported row so re-runs never duplicate data.
- Expose a clean public API (`ILinkedInImporter`) that consumers can call without knowing internal details.

**Non-Goals:**
- No CLI, background service, or scheduled execution.
- No cloud/blob storage — only local file system.
- No authentication or multi-user scoping.
- No data transformation beyond type inference and normalization.

## Decisions

### 1. Architecture: Vertical Slicing + Screaming Architecture + DDD + Clean Architecture

**Decision:** Organize the library using **Vertical Slices** where each top-level folder is named after its Feature/Use Case (Screaming Architecture). Within each feature folder the structure is **flat** — no sub-folders. The Clean Architecture layer and DDD type are encoded directly in the **file name and class name** using dot-segment notation: `{ClassName}.{Layer}.{Type}.cs`. Each feature is fully self-contained and has no compile-time dependency on any other feature folder.

**File naming convention:**

| Segment | Values | Meaning |
|---|---|---|
| `{Layer}` | `Domain` / `Application` / `Infrastructure` | Clean Architecture layer |
| `{Type}` | `Entity`, `ValueObject`, `Contract`, `UseCase` | DDD / pattern role |

Examples: `DiscoveredArchive.Domain.ValueObject.cs`, `IZipDiscovery.Application.Contract.cs`, `IngestZipsUseCase.Application.UseCase.cs`, `ZipDiscovery.Infrastructure.cs`

**Folder layout:**
```
LinkedIn.Data.Import/
  Features/
    ZipIngestion/                                     ← flat, no sub-folders
      DiscoveredArchive.Domain.ValueObject.cs
      IZipDiscovery.Application.Contract.cs
      IZipExtractor.Application.Contract.cs
      ITempDirectoryScope.Application.Contract.cs
      IngestZipsUseCase.Application.UseCase.cs
      ZipDiscovery.Infrastructure.cs
      ZipExtractor.Infrastructure.cs
      TempDirectoryScope.Infrastructure.cs
    SchemaInference/
      InferredSchema.Domain.ValueObject.cs
      ColumnDefinition.Domain.ValueObject.cs
      ICsvSchemaInferrer.Application.Contract.cs
      InferSchemaUseCase.Application.UseCase.cs
      CsvSchemaInferrer.Infrastructure.cs
      TypeDetector.Infrastructure.cs
      TableNameDeriver.Infrastructure.cs
    TableBootstrapping/
      IImportLogBootstrapper.Application.Contract.cs
      ITableBootstrapper.Application.Contract.cs
      ISchemaEvolver.Application.Contract.cs
      BootstrapTablesUseCase.Application.UseCase.cs
      ImportLogBootstrapper.Infrastructure.cs
      TableBootstrapper.Infrastructure.cs
      SchemaEvolver.Infrastructure.cs
    ImportTracking/
      ImportLogEntry.Domain.Entity.cs
      IImportLogRepository.Application.Contract.cs
      IRowHasher.Application.Contract.cs
      TrackImportUseCase.Application.UseCase.cs
      ImportLogRepository.Infrastructure.cs
      RowHasher.Infrastructure.cs
    IncrementalImport/
      ICsvFileImporter.Application.Contract.cs
      ImportCsvFileUseCase.Application.UseCase.cs
      CsvFileImporter.Infrastructure.cs
  Shared/
    ILinkedInImporter.cs             ← public entry-point contract
    ImportOptions.cs
    ImportResult.cs                  ← top-level response (IS a Result)
    FileImportResult.cs              ← per-file response
    ImportError.cs                   ← error detail record
    Result.cs                        ← generic Result<T> and non-generic Result
    ErrorCode.cs                     ← ErrorCode enum (all known failure codes)
    IDomainEvent.cs                  ← marker interface for all domain events
    IEventDispatcher.cs              ← contract: PublishAsync<TEvent>(TEvent, CancellationToken)
    ArchiveExtractedEvent.cs         ← published by ZipIngestion after each ZIP is extracted
    CsvSchemaInferredEvent.cs        ← published by SchemaInference after each schema is ready
    TableReadyEvent.cs               ← published by TableBootstrapping when table is confirmed
    FileImportCompletedEvent.cs      ← published by IncrementalImport after each CSV file finishes
    ImportSessionCompletedEvent.cs   ← published by orchestrator after the full run finishes
    CsvProcessingJob.cs              ← written to Channel by ZipIngestion; read by import pipeline
  LinkedInImporter.cs                ← top-level orchestrator implementing ILinkedInImporter
  InProcessEventDispatcher.cs        ← Infrastructure impl of IEventDispatcher (keyed-handler registry)
  ServiceCollectionExtensions.cs     ← DI registration
```

**Dependency rule (Clean Architecture):** Files whose name contains `.Domain.` have no dependencies outside their feature. Files with `.Application.` depend only on `.Domain.` types and `Shared`. Files with `.Infrastructure.` depend on `.Application.Contract.` and `.Domain.` files within the same feature. Cross-feature communication goes through `Shared` only — never through direct feature-to-feature file references.

**Rationale:** Screaming Architecture makes the codebase self-documenting — opening the project immediately communicates the five business capabilities. Vertical slices allow each feature to evolve, be tested, and be replaced independently without touching other slices. DDD layers within each slice keep business rules (Domain) free from framework and I/O concerns (Infrastructure).

**Alternatives considered:**
- *Horizontal layering (Controllers / Services / Repositories across all features)* — obscures intent; changes to one feature require touching multiple horizontal layers.
- *A single flat namespace* — was the prior approach; discarded because it mixes concerns and makes independent feature evolution difficult.

---

### 2. CSV Parsing: CsvHelper

**Decision:** Use `CsvHelper` (NuGet) for all CSV reading.

**Rationale:** Battle-tested, handles edge cases (quoted fields, multi-line values, BOM), and supports dynamic record reading without a pre-defined model class — critical since schemas are inferred at runtime.

**Alternatives considered:**
- *Manual string splitting* — fragile; breaks on quoted commas and multi-line cells.
- *TextFieldParser (VB.NET)*  — available in .NET but not idiomatic in modern C# and lacks the flexible dynamic API.

---

### 3. Schema Inference: Sample-based Type Detection

**Decision:** Read up to the first 200 rows of each CSV to infer column types. Apply this priority order: `int` → `long` → `decimal` → `DateTimeOffset` → `bool` → `string` (fallback).

**Rationale:** 200 rows is enough to catch type variety without loading entire large files. String is always a safe fallback — widening is never needed post-inference.

**Alternatives considered:**
- *Full-file scan* — unnecessary overhead; LinkedIn exports are not highly heterogeneous within a column.
- *Schema registry / pre-defined mappings* — couples the library to specific LinkedIn export versions; breaks when LinkedIn changes field names.

---

### 4. Database Access: Dapper + Raw DDL String Building

**Decision:** Use `Dapper` for query execution and raw string construction for DDL (`CREATE TABLE IF NOT EXISTS`).

**Rationale:** Table creation and bulk insert are the only operations needed. Dapper gives parameterized queries for DML with minimal overhead, while DDL generation benefits from explicit string construction (no ORM abstraction needed for CREATE TABLE).

**Alternatives considered:**
- *Entity Framework Core* — heavy for dynamic schema generation; EF migrations don't suit runtime-inferred schemas.
- *ADO.NET only* — viable but more verbose for the DML portions; Dapper adds negligible overhead and improves maintainability.

---

### 5. Row Deduplication: SHA-256 Hash of Normalized Row Content

**Decision:** Hash the concatenated, trimmed cell values of each row (pipe-separated, column-ordered) with SHA-256. Store hash + source file name + import timestamp in an `import_log` table.

**Rationale:** Content-based hashing handles re-exported files where row order or file name may change. SHA-256 collision probability is negligible for this data volume.

**Alternatives considered:**
- *Row number + file name* — brittle; LinkedIn export re-runs can renumber rows.
- *Primary key from CSV* — LinkedIn CSVs don't guarantee a stable PK-equivalent column across all export types.

---

### 6. Public API: `ILinkedInImporter` Interface

**Decision:** Expose a single entry point via `ILinkedInImporter.ImportAsync(ImportOptions options, CancellationToken ct)` returning `ImportResult`. The method **never throws** for any known failure condition — all foreseeable errors are captured inside `ImportResult` via the Response Pattern (see Decision 7). The sole legitimate throw is `ArgumentNullException` when `options` is `null` (a programmer error).

**Rationale:** Consumers only need one method. `ImportOptions` carries the ZIPs root path and connection string so the library has no ambient configuration dependency. `CancellationToken` supports graceful cancellation for long-running large exports. Returning all errors inside the result object means callers use a simple `if (!result.IsSuccess)` pattern rather than catching exception types.

---

### 7. Error Handling: Response Pattern

**Decision:** All known, foreseeable failure conditions are communicated through a **Response Pattern** — never thrown as exceptions. The central type is `Result<T>` (and non-generic `Result`) in `Shared/`. Exceptions are reserved exclusively for *unexpected* conditions the developer cannot reasonably anticipate (e.g., `OutOfMemoryException`, stack overflow, unexpected null dereference from a framework call).

**`Result<T>` shape:**
```csharp
Result<T>.Ok(T value)                        // success path
Result<T>.Fail(ErrorCode code, string msg)   // known failure path
result.IsSuccess   // bool
result.Value       // T — only valid when IsSuccess is true
result.ErrorCode   // ErrorCode enum
result.ErrorMessage // string
```

**`ErrorCode` enum** provides stable, programmable identifiers so callers `switch` on codes — never parse message strings.

Known conditions returned as `Result.Fail(...)` (never thrown):

| `ErrorCode` | Condition |
|---|---|
| `RootDirectoryNotFound` | Configured root directory does not exist |
| `NoArchivesFound` | Directory exists but has no recognizable ZIPs |
| `SingleArchiveTypeOnly` | Warning — only one of Basic/Complete present |
| `ArchiveCorrupt` | ZIP cannot be opened or is malformed |
| `CsvParseFailure` | CSV file cannot be read or is malformed |
| `SchemaInferenceFailure` | Cannot derive a usable schema from the CSV |
| `DatabaseConnectionFailure` | Cannot open a database connection |
| `TableCreationFailure` | DDL execution failed |
| `RowInsertFailure` | Row insert failed; transaction rolled back for that file |

**Truly exceptional (may still throw):**
- `ArgumentNullException` — `ImportOptions` is `null` (programmer error)
- `OperationCanceledException` — `CancellationToken` cancelled (expected .NET convention; callers must handle)
- Unexpected framework exceptions (`OutOfMemoryException`, etc.) — cannot be meaningfully caught and returned

**`ImportResult` and `FileImportResult`** are the top-level response objects. `ImportResult.IsSuccess` is `false` when any file failed or a fatal pre-flight check failed. Individual file outcomes live in `FileImportResult` (each with its own `Result` state).

**Rationale:** Business-expected failures (missing directory, corrupt ZIP, DB write error) are not exceptional — they are foreseeable operating conditions. Modeling them as `Result` makes error-handling explicit and compile-time visible, avoids the performance cost of exception stack unwinding on hot paths, and removes the need for callers to know which exception types to catch.

**Alternatives considered:**
- *Exception-based throughout* — prior approach; discarded: callers must know exception types, hot-path overhead, expected conditions treated as exceptional.
- *Out-parameters for errors* — unergonomic in async C# and not composable.

---

### 8. Cross-Feature Communication: Domain Events

**Decision:** Use **Domain Events** for all cross-feature notifications within the import pipeline. Features publish events via `IEventDispatcher` (in `Shared/`); handlers in consuming features subscribe at startup. Events are plain records implementing `IDomainEvent`. The in-process dispatcher (`InProcessEventDispatcher`) dispatches synchronously-async using a keyed handler registry — no external bus or broker.

**Events and their producers/consumers:**

| Event | Published by | Consumed by | Purpose |
|---|---|---|---|
| `ArchiveExtractedEvent` | `ZipIngestion` | `LinkedInImporter` (orchestrator) | Signals that extracted CSV paths are ready; writes a `CsvProcessingJob` to the pipeline channel |
| `CsvSchemaInferredEvent` | `SchemaInference` | `TableBootstrapping` | Triggers table creation/evolution for the inferred schema |
| `TableReadyEvent` | `TableBootstrapping` | `IncrementalImport` | Signals table is confirmed and importable; arms the row-insert pipeline |
| `FileImportCompletedEvent` | `IncrementalImport` | `LinkedInImporter` (orchestrator) | Contributes `FileImportResult` to the running `ImportResult` aggregate |
| `ImportSessionCompletedEvent` | `LinkedInImporter` | External consumers (optional) | Final signal; carries the completed `ImportResult` |

**Row-level events are intentionally omitted:** Publishing one event per inserted row on large exports (100k+) would create write amplification with no cross-feature decoupling benefit. Row tracking is handled internally within `ImportTracking` in the same transaction.

**`IEventDispatcher` contract:**
```csharp
Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
    where TEvent : IDomainEvent;
```

**Rationale:** Domain Events decouple the five feature slices — `SchemaInference` does not import `TableBootstrapping`; it simply publishes a fact. This keeps each slice independently testable. The in-process implementation keeps things simple (no MediatR dependency); it can be swapped for a real bus later if needed.

**Alternatives considered:**
- *Direct method calls between features* — creates compile-time coupling between slices, defeating the purpose of vertical slicing.
- *MediatR* — adds a third-party dependency and reflection overhead for an internal library; overkill at this scope.
- *Row-level `RowImportedEvent`* — rejected; high-volume events with no cross-slice consumer.

---

### 9. Internal Pipeline: `System.Threading.Channels`

**Decision:** Use a **bounded `Channel<CsvProcessingJob>`** as the internal work queue between the ZIP extraction stage and the CSV processing pipeline. `ZipIngestion` acts as the producer — it writes one `CsvProcessingJob` per extracted CSV file into the channel. `IncrementalImport` (via `ImportCsvFileUseCase`) acts as the consumer — it reads jobs from the channel and processes them sequentially (or with bounded parallelism). The channel is created in the orchestrator and injected into both sides.

**`CsvProcessingJob` shape:**
```csharp
record CsvProcessingJob(
    string CsvFilePath,
    string SourceArchiveName,
    ArchiveType ArchiveType   // Basic | Complete
);
```

**Channel configuration:**
- `BoundedChannelOptions` with `FullMode = Wait` and capacity equal to the total number of extracted CSVs (known after extraction, capped at a reasonable max e.g. 128).
- `SingleWriter = true` (only ZipIngestion writes), `SingleReader = false` (allows future parallel consumers).

**Pipeline flow:**
```
ZipIngestion
  └─ extracts ZIP → writes CsvProcessingJob to Channel
                        │
                        ▼
              Channel<CsvProcessingJob>
                        │
                        ▼
  SchemaInference reads job → infers schema
  → publishes CsvSchemaInferredEvent
  → TableBootstrapping creates/evolves table
  → publishes TableReadyEvent
  → IncrementalImport imports rows
  → publishes FileImportCompletedEvent
```

**Rationale:** Channels provide natural backpressure and allow ZIP extraction (I/O-bound) to run concurrently with CSV processing (CPU + DB-bound) without manual thread management. This is a well-understood .NET primitive requiring no external dependencies. The bounded capacity prevents unbounded memory growth if extraction outpaces processing.

**Alternatives considered:**
- *Sequential: extract all, then process all* — simpler but wastes the I/O overlap window; large exports pay full extraction cost before any DB work starts.
- *`Task.WhenAll` with no channel* — no backpressure; risks creating too many concurrent DB connections.
- *`System.Threading.Tasks.Dataflow`* — more powerful but heavier API; overkill for a single producer-consumer pipeline.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| LinkedIn changes CSV column names between export versions | Schema is re-inferred on every run; table columns are additive (new columns added, old ones never dropped) |
| Very large CSVs (100k+ rows) cause memory pressure | Stream rows with `CsvHelper` — never load entire file into memory |
| Hash collisions produce false "already imported" | SHA-256 collision probability ~2⁻¹²⁸ for this data volume; acceptable risk |
| DDL injection via malicious CSV header names | Sanitize column names (strip non-alphanumeric chars, quote identifiers) before DDL generation |
| Partial import failure leaves the database in inconsistent state | Wrap each file's import in a transaction; on failure roll back and capture `Result.Fail(ErrorCode.RowInsertFailure)` in `FileImportResult`; continue with remaining files — no exception propagation |

## Migration Plan

1. Add the `LinkedIn.Data.Import` NuGet (or project reference) to any consumer project.
2. On first run, the library creates all inferred tables plus `import_log` — no manual migration required.
3. Rollback: drop all inferred tables and the `import_log` table; re-run to start fresh (idempotent by design).

## Open Questions

- **Connection string source**: Should `ImportOptions` accept a raw connection string, or an `IDbConnection` factory? → Default to raw connection string for simplicity; can add factory overload later.
- **Multiple export versions in the same directory**: If both a March and April export are present, should both be imported, or only the latest? → Import all present ZIPs; deduplication via hash handles overlap.

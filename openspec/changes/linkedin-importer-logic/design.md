## Context

LinkedIn users can request their data as ZIP exports in two tiers — **Basic** and **Complete**. There is currently no automated mechanism to ingest those exports into our database. All downstream features (analytics, job-tracking, profile enrichment) depend on this ingestion layer existing before any other work proceeds.

The system will be implemented as a standalone C# Class Library (`LinkedIn.Data.Import`) with no UI dependency. Consumers will trigger it programmatically when they detect the user has signaled readiness (e.g., placed ZIPs in the watched directory).

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

### 1. Library Structure: Single Class Library with Internal Modules

**Decision:** One `LinkedIn.Data.Import` Class Library with logically separated internal namespaces (`ZipIngestion`, `SchemaInference`, `TableBootstrapping`, `IncrementalImport`, `ImportTracking`).

**Rationale:** Keeps the public surface small (`ILinkedInImporter`) while allowing each concern to evolve independently. A single library is easier to version and reference than multiple micro-packages at this stage.

**Alternatives considered:**
- *Multiple separate libraries per capability* — over-engineering for a single cohesive feature; increases project complexity with no current consumer benefit.

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

**Decision:** Expose a single entry point via `ILinkedInImporter.ImportAsync(ImportOptions options, CancellationToken ct)` returning `ImportResult`.

**Rationale:** Consumers only need one method. `ImportOptions` carries the ZIPs root path and connection string so the library has no ambient configuration dependency. `CancellationToken` supports graceful cancellation for long-running large exports.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| LinkedIn changes CSV column names between export versions | Schema is re-inferred on every run; table columns are additive (new columns added, old ones never dropped) |
| Very large CSVs (100k+ rows) cause memory pressure | Stream rows with `CsvHelper` — never load entire file into memory |
| Hash collisions produce false "already imported" | SHA-256 collision probability ~2⁻¹²⁸ for this data volume; acceptable risk |
| DDL injection via malicious CSV header names | Sanitize column names (strip non-alphanumeric chars, quote identifiers) before DDL generation |
| Partial import failure leaves the database in inconsistent state | Wrap each file's import in a database transaction; roll back on error, log failure, continue with remaining files |

## Migration Plan

1. Add the `LinkedIn.Data.Import` NuGet (or project reference) to any consumer project.
2. On first run, the library creates all inferred tables plus `import_log` — no manual migration required.
3. Rollback: drop all inferred tables and the `import_log` table; re-run to start fresh (idempotent by design).

## Open Questions

- **Connection string source**: Should `ImportOptions` accept a raw connection string, or an `IDbConnection` factory? → Default to raw connection string for simplicity; can add factory overload later.
- **Multiple export versions in the same directory**: If both a March and April export are present, should both be imported, or only the latest? → Import all present ZIPs; deduplication via hash handles overlap.

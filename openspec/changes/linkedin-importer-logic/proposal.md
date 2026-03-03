## Why

LinkedIn provides data exports in ZIP format, but there is no automated way to ingest that data into our database consistently and safely. Without this foundation, all downstream analytics, job-tracking, and profile enrichment efforts must handle raw file parsing and schema management themselves, leading to duplication, inconsistency, and fragile pipelines.

## What Changes

- **New Class Library** (`LinkedIn.Data.Import`) containing all importer logic — no UI, no CLI.
- **ZIP Discovery & Extraction**: Reads `Basic_LinkedInDataExport` and `Complete_LinkedInDataExport` ZIP archives from a known file-system location and extracts their contents in-memory or to a temp path.
- **Schema Inference**: Inspects CSV headers and a sample of rows to infer column names, data types, and nullability — producing C# model definitions and SQL table schemas.
- **Table Bootstrapping**: Creates database tables from inferred schemas when they do not already exist.
- **Incremental Import**: Inserts only rows that have not been previously imported; existing records are never overwritten or deleted.
- **Import Tracking**: Maintains an import-log table (or equivalent mechanism) recording which rows have been processed, ensuring idempotency across multiple runs.

## Capabilities

### New Capabilities

- `zip-ingestion`: Detects and extracts the two LinkedIn ZIP export archives from a configured directory path.
- `csv-schema-inference`: Parses CSV headers and sample values to infer column types and produce C# model classes and SQL DDL.
- `table-bootstrapping`: Creates database tables from inferred schemas when they do not already exist; no-ops when tables are already present.
- `incremental-import`: Compares incoming CSV rows against import-log records and inserts only net-new rows, guaranteeing idempotency.
- `import-tracking`: Records each imported row (source file, row fingerprint/hash, import timestamp) in a durable import-log store.

### Modified Capabilities

_(none — this is a greenfield module)_

## Impact

- **New project**: `LinkedIn.Data.Import` Class Library (C#/.NET).
- **Database**: Requires a connection to the target database; will create tables on first run.
- **File system**: Requires read access to the directory containing the LinkedIn ZIP files.
- **Dependencies**: CSV parsing library (e.g., `CsvHelper`), a lightweight ORM or raw ADO.NET/Dapper for table creation and inserts, and a hashing utility for row fingerprinting.
- **No breaking changes** to existing code — this is a new, standalone library with no current consumers.

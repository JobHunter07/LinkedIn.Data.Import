## ADDED Requirements

### Requirement: Persist import log entries for every inserted row
For every row successfully inserted into a target table, the system SHALL write one entry to `import_log` containing: the source file name (relative to the ZIP root), the SHA-256 hash of the row content, and the UTC timestamp of import. The entry SHALL be written within the same database transaction as the row insert.

#### Scenario: Row inserted successfully
- **WHEN** a net-new row is inserted into a target table
- **THEN** a corresponding `import_log` record is committed in the same transaction with `source_file`, `row_hash`, and `imported_at` populated

#### Scenario: Transaction rolled back
- **WHEN** a file-level transaction is rolled back due to an error
- **THEN** all `import_log` entries written during that transaction are also rolled back

### Requirement: Import log query for deduplication check
The system SHALL load existing row hashes for a given source file into memory before processing that file, to avoid per-row database round-trips during deduplication.

#### Scenario: Deduplication check uses in-memory hash set
- **WHEN** the importer begins processing a CSV file
- **THEN** all existing `row_hash` values for that `source_file` are fetched from `import_log` once and stored in a `HashSet<string>` for O(1) lookup during row iteration

#### Scenario: Large import log
- **WHEN** the `import_log` table contains millions of entries across many source files
- **THEN** only the hashes for the current source file are loaded, keeping memory usage proportional to one file's history rather than the entire log

### Requirement: Import summary available post-run
The system SHALL return an `ImportResult` object after each invocation summarizing: total files processed, total rows inserted, total rows skipped, total errors, and a per-file breakdown.

#### Scenario: Successful full import
- **WHEN** the importer completes processing all discovered CSV files without errors
- **THEN** `ImportResult.Success` is `true`, `TotalInserted` reflects all new rows, `TotalSkipped` reflects all duplicate rows, and `Errors` is empty

#### Scenario: Partial failure
- **WHEN** one or more files fail during processing
- **THEN** `ImportResult.Success` is `false`, `Errors` contains one entry per failed file with a message and source file name, and successfully processed files are still reflected in the counts

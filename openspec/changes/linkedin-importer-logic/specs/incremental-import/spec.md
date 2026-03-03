## ADDED Requirements

### Requirement: Skip rows already recorded in the import log
The system SHALL compute a SHA-256 hash of each CSV row (column-ordered, pipe-delimited, trimmed) and check whether that hash already exists in `import_log` for the given source file. Rows whose hash is already present SHALL be skipped without inserting a duplicate.

#### Scenario: Row already imported
- **WHEN** a CSV row hash matches an existing `import_log` entry for the same source file
- **THEN** the row is not inserted into the target table and no new `import_log` entry is created

#### Scenario: Row is new
- **WHEN** a CSV row hash does not exist in `import_log` for the source file
- **THEN** the row is inserted into the target table and a corresponding entry is added to `import_log`

#### Scenario: Same ZIP re-imported
- **WHEN** the exact same LinkedIn export ZIP is imported a second time without any new data
- **THEN** zero rows are inserted into any target table and the `ImportResult` reports zero new rows

### Requirement: Net-new row count reported in result
The system SHALL include in `ImportResult` the count of rows inserted per source file and the count of rows skipped (already imported).

#### Scenario: Partial new data
- **WHEN** a CSV contains 500 rows and 300 were previously imported
- **THEN** `ImportResult` shows 200 inserted and 300 skipped for that file

### Requirement: Transactional insert per file
The system SHALL wrap all inserts for a single CSV file in a database transaction. If any insert fails, the transaction for that file SHALL be rolled back in full; the `import_log` entries for that file SHALL also be rolled back.

#### Scenario: Insert error mid-file
- **WHEN** an error occurs while inserting row 150 of 300 for a given CSV
- **THEN** none of the rows from that file are committed to the target table or `import_log`, and the error is recorded in `ImportResult`; other files continue processing

### Requirement: Idempotent across multiple runs
The system SHALL produce identical final database state regardless of how many times it is invoked with the same input data.

#### Scenario: Import run three times with unchanged ZIPs
- **WHEN** the importer is called three times in succession with the same ZIP archives
- **THEN** after the first run all rows are present; subsequent runs insert zero rows and produce no errors

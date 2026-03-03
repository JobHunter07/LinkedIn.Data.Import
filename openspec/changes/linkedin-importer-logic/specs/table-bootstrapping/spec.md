## ADDED Requirements

### Requirement: Create table when it does not exist
The system SHALL create a database table for a CSV when no table with the derived name exists in the target database. The DDL SHALL include all inferred columns, an auto-increment surrogate primary key (`id`), and a `created_at` timestamp column set to the current UTC time on insert.

#### Scenario: Table does not exist on first run
- **WHEN** the importer runs for the first time and no table exists for a given CSV
- **THEN** the table is created with all inferred columns, an `id` primary key, and a `created_at` column before any rows are inserted

#### Scenario: Table already exists
- **WHEN** a table with the derived name already exists in the target database
- **THEN** no DDL is executed for that table; the importer proceeds directly to the incremental import phase

### Requirement: Additive column migration on schema evolution
The system SHALL detect columns present in the current CSV that do not exist in the existing table and SHALL add those columns via `ALTER TABLE ... ADD COLUMN`. It SHALL NEVER drop or rename existing columns.

#### Scenario: New column appears in updated CSV
- **WHEN** a CSV re-export contains a column that was not present when the table was first created
- **THEN** the new column is added to the existing table with the inferred type and nullable constraint before importing rows from this run

#### Scenario: Column removed from updated CSV
- **WHEN** a CSV re-export is missing a column that exists in the database table
- **THEN** the existing column is left untouched in the table; rows from this run will have NULL for that column

### Requirement: Import log table bootstrapped automatically
The system SHALL create an `import_log` table on first run if it does not exist. The table SHALL contain at minimum: `id`, `source_file`, `row_hash` (SHA-256 hex), `imported_at` (UTC timestamp).

#### Scenario: Import log table absent on first run
- **WHEN** no `import_log` table exists in the database
- **THEN** the importer creates it before processing any CSV file

#### Scenario: Import log table already exists
- **WHEN** the `import_log` table already exists
- **THEN** the importer proceeds without attempting to re-create it

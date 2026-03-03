## ADDED Requirements

### Requirement: Column type inference from CSV headers and sample rows
The system SHALL infer the SQL-compatible data type for each CSV column by examining the header row and up to the first 200 data rows. Type priority SHALL be: `INT` → `BIGINT` → `DECIMAL` → `DATETIMEOFFSET` → `BIT` → `NVARCHAR(MAX)` (fallback). A column SHALL be typed to the lowest-precision type that accommodates all sampled non-empty values.

#### Scenario: Integer column detected
- **WHEN** all non-empty sampled values for a column parse as 32-bit integers
- **THEN** the inferred type for that column is `INT`

#### Scenario: Date column detected
- **WHEN** all non-empty sampled values for a column parse as valid `DateTimeOffset` values (ISO 8601 or common LinkedIn date formats)
- **THEN** the inferred type is `DATETIMEOFFSET`

#### Scenario: Mixed types fall back to string
- **WHEN** sampled values for a column include both numeric and non-numeric entries
- **THEN** the inferred type is `NVARCHAR(MAX)`

#### Scenario: All values empty
- **WHEN** all sampled values for a column are empty or whitespace
- **THEN** the inferred type is `NVARCHAR(MAX)` and the column is marked nullable

### Requirement: Table name derived from CSV file name
The system SHALL derive the target database table name from the CSV file name by converting it to snake_case and stripping the `.csv` extension. The derived name SHALL be sanitized to contain only alphanumeric characters and underscores.

#### Scenario: Standard CSV file name
- **WHEN** the CSV file is named `Connections.csv`
- **THEN** the target table name is `connections`

#### Scenario: Multi-word CSV file name
- **WHEN** the CSV file is named `Job Applications.csv`
- **THEN** the target table name is `job_applications`

#### Scenario: CSV file name with special characters
- **WHEN** the CSV file name contains characters other than letters, digits, spaces, and underscores
- **THEN** those characters are stripped and the resulting name is sanitized before use

### Requirement: C# model class generation from inferred schema
The system SHALL produce an in-memory C# class definition for each CSV whose schema has been inferred. Each property SHALL use a CLR type that corresponds to the inferred SQL type. The class name SHALL match the PascalCase form of the table name.

#### Scenario: Model class generated for standard CSV
- **WHEN** schema inference completes for `Connections.csv`
- **THEN** an in-memory `ConnectionsRecord` class (or equivalent dynamic type) is available with one property per column, typed per inference results

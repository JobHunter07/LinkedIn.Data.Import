# CSV Deduplication Feature Specification

## Overview
This specification defines the CSV deduplication feature for the LinkedIn.Data.Import.Cli application. The feature processes LinkedIn export ZIP files, extracts CSV files from multiple archives, and consolidates them by removing duplicate records based on content hashing.

## Context
LinkedIn data exports are delivered as ZIP archives containing CSV files. Users may have multiple export archives (e.g., "Basic" and "Complete" exports). The deduplication process ensures that when multiple archives contain overlapping data, only unique records are retained in the consolidated output.

---

## ADDED Requirements

### Requirement: Dedup CSVs menu option exists
The main menu SHALL include a "Dedup CSVs" option that:
1. Is visible when settings are configured
2. Is disabled (dimmed) when settings are not configured
3. Shows descriptive text: "Deduplicate CSV files from ZIP folders"

#### Scenario: Option visible when settings configured
- **WHEN** the user has configured the ZIP directory and connection string
- **THEN** the "Dedup CSVs" menu option is enabled and selectable

#### Scenario: Option disabled when settings not configured
- **WHEN** the user has not configured settings
- **THEN** the "Dedup CSVs" menu option is dimmed with explanatory text

---

### Requirement: ZIP archive discovery and validation
The deduplication process SHALL:
1. Scan the configured ZIP root directory for all `.zip` files (recursive search)
2. Validate that at least one valid LinkedIn export ZIP archive is found
3. Display a list of discovered archives with file names and sizes

#### Scenario: Multiple archives discovered
- **WHEN** the ZIP root directory contains 2 or more `.zip` files
- **THEN** all archives are listed with their names and file sizes

#### Scenario: No archives found
- **WHEN** the ZIP root directory contains no `.zip` files
- **THEN** an error message is displayed and the process exits gracefully

#### Scenario: Single archive found
- **WHEN** only one `.zip` file is found
- **THEN** a warning is displayed indicating deduplication requires multiple archives, and the user is prompted to continue or cancel

---

### Requirement: CSV extraction to temporary workspace
The process SHALL:
1. Create a temporary working directory with a timestamp-based name
2. Extract all CSV files from all discovered ZIP archives to the temporary directory
3. Organize extracted files by source archive (subdirectories named after each ZIP file)
4. Display extraction progress using Spectre.Console progress bars

#### Scenario: Successful extraction
- **WHEN** all archives are processed successfully
- **THEN** CSV files are organized in `{temp}\{archive-name}\*.csv` structure

#### Scenario: Archive extraction failure
- **WHEN** a ZIP archive is corrupted or cannot be read
- **THEN** an error is logged, other archives continue processing, and a summary shows which archives failed

---

### Requirement: Content-based deduplication algorithm
The deduplication logic SHALL:
1. Group CSV files by filename across all source archives
2. For each CSV file group (e.g., all "Connections.csv" files):
   - Read all rows from all source files with that name
   - Calculate a hash for each row using the same `IRowHasher` as the import process
   - Keep only the first occurrence of each unique hash
   - Write deduplicated rows to an output CSV file
3. Preserve CSV headers exactly as they appear in the source files

#### Scenario: Identical rows removed
- **WHEN** "Connections.csv" from Archive A and Archive B contain 3 identical rows
- **THEN** the output "Connections.csv" contains only 3 rows (not 6)

#### Scenario: Unique rows retained
- **WHEN** "Connections.csv" from Archive A has 5 unique rows and Archive B has 3 different unique rows
- **THEN** the output "Connections.csv" contains all 8 rows

#### Scenario: Mixed duplicates and unique rows
- **WHEN** Archive A has rows [1,2,3,4,5] and Archive B has rows [3,4,5,6,7]
- **THEN** the output contains rows [1,2,3,4,5,6,7] (7 unique rows)

---

### Requirement: Output directory structure
The deduplicated CSV files SHALL be written to:
- **Path:** `{ZIP-root-directory}\Deduplicated_{timestamp}\`
- **Structure:** Flat directory containing one CSV file per unique filename found across all archives
- **Naming:** Original CSV filenames are preserved (e.g., "Connections.csv", "Profile.csv")

#### Scenario: Output directory created
- **WHEN** deduplication completes successfully
- **THEN** a new directory is created with name format `Deduplicated_yyyyMMdd_HHmmss`

#### Scenario: Output files written
- **WHEN** deduplication processes 3 unique CSV file types across all archives
- **THEN** 3 CSV files are written to the output directory

---

### Requirement: Progress visualization during deduplication
The process SHALL display:
1. Overall progress: "Processing file X of Y"
2. Per-file progress: Number of rows processed, duplicates found, unique rows retained
3. Live status using Spectre.Console Status/Progress APIs

#### Scenario: Progress updates displayed
- **WHEN** the deduplication is running
- **THEN** the console shows current file name, row counts, and percentage complete

#### Scenario: Completion summary shown
- **WHEN** deduplication completes
- **THEN** a summary table shows:
  - Total archives processed
  - Total CSV files processed
  - Total rows read
  - Total duplicates removed
  - Total unique rows written
  - Output directory path

---

### Requirement: Results summary table
After completion, the CLI SHALL render a Spectre.Console Table showing:
- **Column 1:** CSV file name
- **Column 2:** Total rows read (from all archives)
- **Column 3:** Duplicate rows removed
- **Column 4:** Unique rows written

With a totals row at the bottom.

#### Scenario: Per-file statistics displayed
- **WHEN** deduplication processes "Connections.csv" with 100 total rows and 25 duplicates
- **THEN** the table shows: Connections.csv | 100 | 25 | 75

#### Scenario: Totals row displayed
- **WHEN** all files are processed
- **THEN** the bottom row shows sum of all rows read, duplicates removed, and unique rows written

---

### Requirement: Error handling and partial success
The process SHALL:
1. Continue processing remaining files if a single CSV file fails
2. Log all errors with file names and error details
3. Display a summary of successful and failed files
4. Return a non-zero exit code if any files failed

#### Scenario: Partial success handled gracefully
- **WHEN** 5 CSV files are processed and 1 fails due to malformed data
- **THEN** 4 files are successfully deduplicated and the error is logged with details

#### Scenario: Error panel displayed for failures
- **WHEN** one or more CSV files fail to process
- **THEN** an error panel lists each failed file with error code and message

---

### Requirement: Temporary file cleanup
The process SHALL:
1. Clean up the temporary extraction directory after completion
2. Retain the output directory with deduplicated files
3. Handle cleanup errors gracefully without affecting the deduplication results

#### Scenario: Temp directory removed on success
- **WHEN** deduplication completes successfully
- **THEN** the temporary extraction directory is deleted

#### Scenario: Temp directory removed on failure
- **WHEN** deduplication fails or is cancelled
- **THEN** the temporary extraction directory is still deleted (best effort)

---

### Requirement: Cancellation support
The user SHALL be able to cancel the deduplication process at any time by pressing Ctrl+C. The process SHALL:
1. Stop processing gracefully
2. Clean up temporary files
3. Display a cancellation message
4. Exit with code 130 (standard cancellation exit code)

#### Scenario: User cancels during extraction
- **WHEN** the user presses Ctrl+C during ZIP extraction
- **THEN** extraction stops, temp files are cleaned up, and process exits with code 130

#### Scenario: User cancels during deduplication
- **WHEN** the user presses Ctrl+C while processing CSV files
- **THEN** current file processing stops, partial output is discarded, and process exits with code 130

---

### Requirement: Integration with existing settings
The deduplication feature SHALL:
1. Use the ZIP root directory from the settings menu
2. Not require a database connection string
3. Validate that the ZIP directory setting exists before enabling the menu option

#### Scenario: Uses configured ZIP directory
- **WHEN** the user has set ZIP directory to "H:\Job-Hunt-2026\Resources"
- **THEN** deduplication scans that directory for ZIP files

#### Scenario: No database connection required
- **WHEN** deduplication runs
- **THEN** it does not attempt to connect to SQL Server or validate the connection string

---

### Requirement: Output directory is ready for import
The deduplicated CSV files SHALL be compatible with the LinkedIn Import feature. After deduplication:
1. User can point the import process to the deduplicated output directory
2. No duplicate rows will be encountered during import
3. File structure and format remain valid for the import pipeline

#### Scenario: Deduplicated files importable
- **WHEN** the user runs import pointing to the deduplicated output directory
- **THEN** all CSV files are successfully imported without hash-based skips

---

## Implementation Notes

### Dependencies
- `System.IO.Compression` for ZIP extraction
- `CsvHelper` for CSV parsing (already in project)
- `IRowHasher` from existing import infrastructure
- `Spectre.Console` for UI
- `IZipDiscovery` and `IZipExtractor` (may need to be extracted from library if currently internal)

### Service Registration
The feature should be implemented as:
- `DedupOrchestrator` class to coordinate the workflow
- `CsvDeduplicator` service to handle per-file deduplication logic
- Extension method to register services: `AddCsvDeduplication()`

### File Hashing Strategy
Use the existing `IRowHasher` implementation to ensure hash consistency with the import process. The hash algorithm must produce identical results for identical row data.

### Memory Management
For large CSV files (>100MB), consider streaming row-by-row rather than loading entire files into memory. Use `HashSet<string>` to track seen hashes efficiently.

---

## Future Enhancements (Out of Scope)

The following features are NOT included in this specification but may be considered for future versions:

1. **Smart merge:** When duplicate rows have different data in some columns, apply merge logic (e.g., keep most recent timestamp)
2. **Differential deduplication:** Only process archives that haven't been deduplicated before (incremental mode)
3. **Compression:** Create a ZIP archive of the deduplicated output
4. **Configuration:** Allow user to specify custom output directory path
5. **Dry run mode:** Preview what would be deduplicated without writing output files

---

## Success Criteria

The deduplication feature is considered complete when:

1. ✅ User can select "Dedup CSVs" from the main menu
2. ✅ All ZIP archives in the configured directory are discovered and processed
3. ✅ Duplicate rows are correctly identified and removed using content hashing
4. ✅ Output directory is created with deduplicated CSV files
5. ✅ Results summary table displays accurate statistics
6. ✅ Process handles errors gracefully and provides clear error messages
7. ✅ Temporary files are cleaned up after completion
8. ✅ Deduplicated output is compatible with the import process
9. ✅ All scenarios in this specification pass validation

---

## Acceptance Tests

### Test Suite: End-to-End Deduplication
1. Given 2 ZIP archives with overlapping "Connections.csv" files
2. When user runs deduplication
3. Then output contains exactly the unique set of connections
4. And results table shows correct counts

### Test Suite: Error Handling
1. Given a corrupted ZIP archive
2. When user runs deduplication
3. Then valid archives are processed successfully
4. And corrupted archive is reported in error panel

### Test Suite: Single Archive Warning
1. Given only 1 ZIP archive
2. When user runs deduplication
3. Then a warning is displayed about needing multiple archives
4. And user can choose to continue or cancel

---

**Version:** 1.0  
**Status:** Draft  
**Author:** LinkedIn.Data.Import Team  
**Date:** 2026-01-20

## ADDED Requirements

### Requirement: ZIP archive discovery
The system SHALL discover all LinkedIn export ZIP archives located in a configured root directory. It SHALL recognize archives whose filenames begin with `Basic_LinkedInDataExport` or `Complete_LinkedInDataExport` (case-insensitive). It SHALL raise a descriptive error if the root directory does not exist or contains no recognizable archives.

#### Scenario: Both archive types are present
- **WHEN** the root directory contains at least one `Basic_LinkedInDataExport*.zip` and one `Complete_LinkedInDataExport*.zip`
- **THEN** both archives are discovered and queued for processing

#### Scenario: Only one archive type is present
- **WHEN** the root directory contains archives of only one type
- **THEN** the importer processes those archives and logs a warning that the other type was not found; it SHALL NOT fail

#### Scenario: No recognizable archives found
- **WHEN** the root directory exists but contains no files matching either naming pattern
- **THEN** the importer returns an error result indicating no archives were found and performs no database operations

#### Scenario: Root directory does not exist
- **WHEN** the configured root directory path does not exist on the file system
- **THEN** the importer throws an `ImportConfigurationException` before any extraction begins

### Requirement: ZIP extraction to temporary location
The system SHALL extract the contents of each discovered ZIP archive into a temporary working directory before processing. It SHALL clean up the temporary directory after processing completes, whether successfully or not.

#### Scenario: Successful extraction
- **WHEN** a valid ZIP archive is discovered
- **THEN** all contained files are extracted to a temporary directory, the extracted file paths are made available for downstream processing, and the temporary directory is deleted when import finishes

#### Scenario: Corrupt or unreadable ZIP
- **WHEN** a discovered archive cannot be opened or is corrupt
- **THEN** that archive is skipped with an error entry in the result, remaining archives are still processed, and the temporary directory is cleaned up

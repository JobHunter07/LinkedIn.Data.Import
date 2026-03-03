namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Stable, programmable error identifiers for all known failure conditions in
/// the LinkedIn data import pipeline. Callers should switch on these codes rather
/// than parsing error message strings.
/// </summary>
public enum ErrorCode
{
    /// <summary>The configured root directory does not exist on the file system.</summary>
    RootDirectoryNotFound,

    /// <summary>The root directory exists but contains no recognisable LinkedIn ZIP archives.</summary>
    NoArchivesFound,

    /// <summary>Only one archive type (Basic or Complete) was found — the other is absent.</summary>
    SingleArchiveTypeOnly,

    /// <summary>A ZIP archive could not be opened or is malformed.</summary>
    ArchiveCorrupt,

    /// <summary>A CSV file could not be read or is malformed.</summary>
    CsvParseFailure,

    /// <summary>No usable schema could be derived from the CSV file.</summary>
    SchemaInferenceFailure,

    /// <summary>A database connection could not be established.</summary>
    DatabaseConnectionFailure,

    /// <summary>DDL execution for table creation or alteration failed.</summary>
    TableCreationFailure,

    /// <summary>A row insert failed; the file-level transaction was rolled back.</summary>
    RowInsertFailure,
}

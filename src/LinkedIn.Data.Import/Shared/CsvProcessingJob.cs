namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// A work-item written to the internal <c>Channel&lt;CsvProcessingJob&gt;</c>
/// by the ZIP-ingestion stage and consumed by the CSV-processing pipeline.
/// </summary>
/// <param name="CsvFilePath">Absolute path to the extracted CSV file.</param>
/// <param name="SourceArchiveName">File name of the archive from which the CSV was extracted.</param>
/// <param name="ArchiveType">Whether the archive is Basic or Complete.</param>
public sealed record CsvProcessingJob(
    string CsvFilePath,
    string SourceArchiveName,
    ArchiveType ArchiveType);

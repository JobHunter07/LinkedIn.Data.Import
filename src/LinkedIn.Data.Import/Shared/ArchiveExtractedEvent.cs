namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Published by the ZIP-ingestion feature after a single archive has been
/// successfully extracted.
/// </summary>
/// <param name="ArchiveName">File name of the extracted archive.</param>
/// <param name="ExtractedCsvPaths">Absolute paths of all CSV files extracted from the archive.</param>
public sealed record ArchiveExtractedEvent(
    string ArchiveName,
    IReadOnlyList<string> ExtractedCsvPaths) : IDomainEvent;

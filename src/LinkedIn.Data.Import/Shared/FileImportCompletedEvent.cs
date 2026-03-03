namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Published by the incremental-import feature after a single CSV file has
/// finished processing (success or failure).
/// </summary>
/// <param name="SourceFile">The CSV file path (relative to the archive root).</param>
/// <param name="Result">The per-file import outcome.</param>
public sealed record FileImportCompletedEvent(
    string SourceFile,
    FileImportResult Result) : IDomainEvent;

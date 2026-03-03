namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Describes a single error that occurred during import processing.
/// </summary>
/// <param name="SourceFile">The CSV or archive file where the error occurred, relative to the ZIP root.</param>
/// <param name="Code">The stable error code identifying the failure kind.</param>
/// <param name="Message">A human-readable description of the error.</param>
public sealed record ImportError(
    string SourceFile,
    ErrorCode Code,
    string Message);

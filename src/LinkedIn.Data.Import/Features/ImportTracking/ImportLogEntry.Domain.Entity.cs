namespace LinkedIn.Data.Import.Features.ImportTracking;

/// <summary>
/// Represents a single row in the <c>import_log</c> table.
/// </summary>
public sealed class ImportLogEntry
{
    /// <summary>Surrogate primary key.</summary>
    public long Id { get; init; }

    /// <summary>Source file path relative to the ZIP root.</summary>
    public required string SourceFile { get; init; }

    /// <summary>SHA-256 hex fingerprint of the row content.</summary>
    public required string RowHash { get; init; }

    /// <summary>UTC timestamp when the row was imported.</summary>
    public DateTimeOffset ImportedAt { get; init; }
}

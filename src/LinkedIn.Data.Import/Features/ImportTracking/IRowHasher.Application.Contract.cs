namespace LinkedIn.Data.Import.Features.ImportTracking;

/// <summary>
/// Computes a stable content fingerprint for a CSV row.
/// </summary>
public interface IRowHasher
{
    /// <summary>
    /// Concatenates <paramref name="values"/> (trimmed, pipe-delimited,
    /// column-ordered) and returns the SHA-256 digest as a lowercase hex string.
    /// </summary>
    string Hash(IEnumerable<string?> values);
}

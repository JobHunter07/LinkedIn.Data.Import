using System.Security.Cryptography;
using System.Text;

namespace LinkedIn.Data.Import.Features.ImportTracking;

/// <summary>
/// SHA-256-based implementation of <see cref="IRowHasher"/>.
/// Produces a stable lowercase hex fingerprint for any ordered set of
/// cell values.
/// </summary>
public sealed class RowHasher : IRowHasher
{
    /// <inheritdoc/>
    public string Hash(IEnumerable<string?> values)
    {
        // Trim each value, join with pipes. Null cells treated as empty string.
        var content = string.Join("|", values.Select(v => (v ?? string.Empty).Trim()));
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

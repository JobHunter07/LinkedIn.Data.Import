using System.Globalization;

namespace LinkedIn.Data.Import.Features.SchemaInference;

/// <summary>
/// Determines the best SQL data type for a column based on its sampled values.
/// Priority order: <c>INT</c> → <c>BIGINT</c> → <c>DECIMAL</c> →
/// <c>DATETIMEOFFSET</c> → <c>BIT</c> → <c>NVARCHAR(MAX)</c>.
/// </summary>
public sealed class TypeDetector
{
    /// <summary>
    /// Infers the SQL type for <paramref name="values"/> and returns both the
    /// SQL type string and the corresponding CLR type.
    /// </summary>
    /// <param name="values">The non-null sampled cell values for the column.</param>
    /// <param name="sqlType">The inferred SQL type string.</param>
    /// <param name="clrType">The corresponding .NET CLR type.</param>
    /// <param name="isNullable">
    /// <see langword="true"/> when every sampled value is empty or whitespace.
    /// </param>
    public void Infer(
        IEnumerable<string> values,
        out string sqlType,
        out Type clrType,
        out bool isNullable)
    {
        var list = values.ToList();
        var nonEmpty = list.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        // Consider a column nullable when any sampled value is empty or whitespace.
        // This avoids creating NOT NULL constraints when CSVs contain missing cells.
        isNullable = list.Any(v => string.IsNullOrWhiteSpace(v));

        if (nonEmpty.Count == 0)
        {
            sqlType = "NVARCHAR(MAX)";
            clrType = typeof(string);
            return;
        }

        if (nonEmpty.All(v => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            sqlType = "INT";
            clrType = typeof(int);
            return;
        }

        if (nonEmpty.All(v => long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            sqlType = "BIGINT";
            clrType = typeof(long);
            return;
        }

        if (nonEmpty.All(v => decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
        {
            sqlType = "DECIMAL(18,6)";
            clrType = typeof(decimal);
            return;
        }

        if (nonEmpty.All(v => DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _)))
        {
            sqlType = "DATETIMEOFFSET";
            clrType = typeof(DateTimeOffset);
            return;
        }

        if (nonEmpty.All(v => v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                               v.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                               v == "1" || v == "0"))
        {
            sqlType = "BIT";
            clrType = typeof(bool);
            return;
        }

        sqlType = "NVARCHAR(MAX)";
        clrType = typeof(string);
    }
}

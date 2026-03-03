using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.SchemaInference;

/// <summary>
/// Infers the SQL schema of a CSV file by reading its headers and a sample of rows.
/// </summary>
public interface ICsvSchemaInferrer
{
    /// <summary>
    /// Opens <paramref name="csvFilePath"/>, reads headers and up to 200 data rows,
    /// and infers a SQL schema.
    /// </summary>
    /// <returns>
    /// The inferred schema on success, or
    /// <see cref="ErrorCode.CsvParseFailure"/> /
    /// <see cref="ErrorCode.SchemaInferenceFailure"/> on failure.
    /// </returns>
    Task<Result<InferredSchema>> InferAsync(
        string csvFilePath,
        CancellationToken cancellationToken = default);
}

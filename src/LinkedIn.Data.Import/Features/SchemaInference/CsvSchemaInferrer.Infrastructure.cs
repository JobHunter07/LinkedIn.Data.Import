using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.SchemaInference;

/// <summary>
/// CsvHelper-backed implementation of <see cref="ICsvSchemaInferrer"/>.
/// Reads up to 200 data rows to infer column types.
/// </summary>
public sealed class CsvSchemaInferrer : ICsvSchemaInferrer
{
    private const int MaxSampleRows = 200;

    private readonly TypeDetector _typeDetector;
    private readonly TableNameDeriver _tableNameDeriver;
    private readonly IEventDispatcher _events;

    /// <summary>Initialises the inferrer with its collaborators.</summary>
    public CsvSchemaInferrer(
        TypeDetector typeDetector,
        TableNameDeriver tableNameDeriver,
        IEventDispatcher events)
    {
        _typeDetector = typeDetector;
        _tableNameDeriver = tableNameDeriver;
        _events = events;
    }

    /// <inheritdoc/>
    public async Task<Result<InferredSchema>> InferAsync(
        string csvFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
            };

            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, config);

            // Read header row.
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            var headers = csv.HeaderRecord;
            if (headers is null || headers.Length == 0)
                return Result<InferredSchema>.Fail(
                    ErrorCode.SchemaInferenceFailure,
                    $"CSV file '{csvFilePath}' has no headers.");

            // Collect sampled values per column.
            var samples = headers.Select(_ => new List<string>()).ToArray();
            int rowsRead = 0;

            while (rowsRead < MaxSampleRows && await csv.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = 0; i < headers.Length; i++)
                {
                    var value = csv.GetField(i) ?? string.Empty;
                    samples[i].Add(value.Trim());
                }
                rowsRead++;
            }

            var tableName = _tableNameDeriver.Derive(csvFilePath);

            var columns = headers.Select((header, i) =>
            {
                _typeDetector.Infer(samples[i], out var sqlType, out var clrType, out var isNullable);
                var sanitisedName = SanitiseColumnName(header);
                return new ColumnDefinition(sanitisedName, sqlType, isNullable, clrType);
            }).ToList();

            var schema = new InferredSchema(tableName, columns);

            await _events.PublishAsync(
                new CsvSchemaInferredEvent(csvFilePath, schema),
                cancellationToken).ConfigureAwait(false);

            return Result<InferredSchema>.Ok(schema);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<InferredSchema>.Fail(
                ErrorCode.CsvParseFailure,
                $"Failed to read CSV '{csvFilePath}': {ex.Message}");
        }
    }

    private static string SanitiseColumnName(string rawHeader)
    {
        // Trim, replace spaces/hyphens with underscores, strip non-alphanumeric.
        var name = rawHeader.Trim().Replace(' ', '_').Replace('-', '_');
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (name.Length > 0 && char.IsDigit(name[0]))
            name = "_" + name;
        return string.IsNullOrEmpty(name) ? "column" : name;
    }
}

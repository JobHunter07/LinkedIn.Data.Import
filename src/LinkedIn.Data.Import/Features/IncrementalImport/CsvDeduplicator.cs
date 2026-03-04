using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LinkedIn.Data.Import.Features.ImportTracking;

namespace LinkedIn.Data.Import.Features.IncrementalImport;

/// <summary>
/// Result of a CSV deduplication operation.
/// </summary>
public sealed class DeduplicationResult
{
    public bool IsSuccess { get; init; }
    public string OriginalFilePath { get; init; } = string.Empty;
    public string DeduplicatedFilePath { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int UniqueRows { get; init; }
    public int DuplicatesRemoved => TotalRows - UniqueRows;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Safely deduplicates CSV files by removing duplicate rows based on hash,
/// creating new "_deduplicated.csv" files without modifying originals.
/// </summary>
public sealed class CsvDeduplicator
{
    private readonly IRowHasher _hasher;

    public CsvDeduplicator(IRowHasher hasher)
    {
        _hasher = hasher;
    }

    /// <summary>
    /// Deduplicates a CSV file, creating a new file with "_deduplicated" suffix.
    /// Original file is never modified.
    /// </summary>
    /// <param name="csvFilePath">Path to the original CSV file</param>
    /// <param name="outputDirectory">Directory where deduplicated file will be created (defaults to same directory as original)</param>
    public async Task<DeduplicationResult> DeduplicateAsync(
        string csvFilePath,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(csvFilePath))
            {
                return new DeduplicationResult
                {
                    IsSuccess = false,
                    OriginalFilePath = csvFilePath,
                    ErrorMessage = $"File not found: {csvFilePath}"
                };
            }

            // Determine output path
            var fileName = Path.GetFileNameWithoutExtension(csvFilePath);
            var extension = Path.GetExtension(csvFilePath);
            var outDir = outputDirectory ?? Path.GetDirectoryName(csvFilePath) ?? Directory.GetCurrentDirectory();
            var deduplicatedPath = Path.Combine(outDir, $"{fileName}_deduplicated{extension}");

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
            };

            var seenHashes = new HashSet<string>();
            var totalRows = 0;
            var uniqueRows = 0;
            string[]? headers = null;

            // Read and deduplicate
            using (var reader = new StreamReader(csvFilePath))
            using (var csv = new CsvReader(reader, config))
            using (var writer = new StreamWriter(deduplicatedPath))
            using (var csvWriter = new CsvWriter(writer, config))
            {
                await csv.ReadAsync();
                csv.ReadHeader();
                headers = csv.HeaderRecord ?? [];

                // Write header to output
                foreach (var header in headers)
                {
                    csvWriter.WriteField(header);
                }
                await csvWriter.NextRecordAsync();

                // Process each row
                while (await csv.ReadAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    totalRows++;

                    var values = headers.Select((_, i) => csv.GetField(i) ?? string.Empty).ToArray();
                    var hash = _hasher.Hash(values);

                    if (seenHashes.Add(hash))
                    {
                        // First occurrence - keep it
                        uniqueRows++;
                        foreach (var value in values)
                        {
                            csvWriter.WriteField(value);
                        }
                        await csvWriter.NextRecordAsync();
                    }
                    // Else: duplicate - skip it
                }
            }

            return new DeduplicationResult
            {
                IsSuccess = true,
                OriginalFilePath = csvFilePath,
                DeduplicatedFilePath = deduplicatedPath,
                TotalRows = totalRows,
                UniqueRows = uniqueRows
            };
        }
        catch (Exception ex)
        {
            return new DeduplicationResult
            {
                IsSuccess = false,
                OriginalFilePath = csvFilePath,
                ErrorMessage = $"Deduplication failed: {ex.Message}"
            };
        }
    }
}

using System.Globalization;
using System.Threading.Channels;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.IncrementalImport;

/// <summary>
/// Reads jobs from the internal channel and imports each CSV file
/// incrementally using hash-based deduplication.
/// </summary>
public sealed class CsvFileImporter : ICsvFileImporter
{
    private readonly ICsvSchemaInferrer _inferrer;
    private readonly ITableBootstrapper _bootstrapper;
    private readonly IImportLogRepository _importLog;
    private readonly IRowHasher _hasher;
    private readonly ISqlDialect _dialect;
    private readonly IEventDispatcher _events;

    /// <summary>Initialises the importer with its collaborators.</summary>
    public CsvFileImporter(
        ICsvSchemaInferrer inferrer,
        ITableBootstrapper bootstrapper,
        IImportLogRepository importLog,
        IRowHasher hasher,
        ISqlDialect dialect,
        IEventDispatcher events)
    {
        _inferrer = inferrer;
        _bootstrapper = bootstrapper;
        _importLog = importLog;
        _hasher = hasher;
        _dialect = dialect;
        _events = events;
    }

    /// <inheritdoc/>
    public async Task RunAsync(
        System.Data.IDbConnection connection,
        ChannelReader<CsvProcessingJob> channelReader,
        CancellationToken cancellationToken = default)
    {
        await foreach (var job in channelReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var fileResult = new FileImportResult { SourceFile = job.CsvFilePath };

            try
            {
                await ProcessJobAsync(connection, job, fileResult, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                fileResult.Errors.Add(new ImportError(
                    job.CsvFilePath,
                    ErrorCode.RowInsertFailure,
                    $"Unexpected error processing '{job.CsvFilePath}': {ex.Message}"));
            }
            finally
            {
                await _events.PublishAsync(
                    new FileImportCompletedEvent(job.CsvFilePath, fileResult),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessJobAsync(
        System.Data.IDbConnection connection,
        CsvProcessingJob job,
        FileImportResult fileResult,
        CancellationToken cancellationToken)
    {
        // 1. Infer schema (publishes CsvSchemaInferredEvent → TableBootstrapper
        //    creates/evolves the table synchronously within the dispatch chain →
        //    publishes TableReadyEvent). By the time InferAsync returns, the table
        //    is confirmed ready.
        var inferResult = await _inferrer.InferAsync(job.CsvFilePath, cancellationToken)
            .ConfigureAwait(false);

        if (!inferResult.IsSuccess)
        {
            fileResult.Errors.Add(new ImportError(
                job.CsvFilePath, inferResult.ErrorCode, inferResult.ErrorMessage));
            return;
        }

        var schema = inferResult.Value;

        // 2. Bootstrap table (no-op when already done by event handler in step 1,
        //    but ensures consistency when called standalone too).
        var bootstrapResult = await _bootstrapper.EnsureTableAsync(
            connection, schema, cancellationToken).ConfigureAwait(false);

        if (!bootstrapResult.IsSuccess)
        {
            fileResult.Errors.Add(new ImportError(
                job.CsvFilePath, bootstrapResult.ErrorCode, bootstrapResult.ErrorMessage));
            return;
        }

        // 3. Load existing hashes for this source file.
        var existingHashes = await _importLog
            .LoadHashSetAsync(connection, Path.GetFileName(job.CsvFilePath), cancellationToken)
            .ConfigureAwait(false);

        // 4. Open a per-file transaction.
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            // 5. Stream CSV rows (do not load entire file into memory).
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
            };

            using var reader = new StreamReader(job.CsvFilePath);
            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            var headers = csv.HeaderRecord ?? [];
            var columnNames = headers.Select(h => SanitiseColumnName(h)).ToArray();

            // Pre-build the INSERT statement.
            var insertSql = BuildInsertSql(schema.TableName, columnNames);

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var values = columnNames.Select((_, i) => csv.GetField(i)).ToArray();
                var hash = _hasher.Hash(values);

                if (existingHashes.Contains(hash))
                {
                    fileResult.SkippedCount++;
                    continue;
                }

                // Insert row.
                var parameters = new DynamicParameters();
                for (int i = 0; i < columnNames.Length; i++)
                    parameters.Add(columnNames[i], values[i]);
                parameters.Add("created_at", DateTimeOffset.UtcNow);

                await connection.ExecuteAsync(
                    new CommandDefinition(insertSql, parameters, transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                // Log the import entry within the same transaction.
                var sourceFileName = Path.GetFileName(job.CsvFilePath);
                await _importLog.RecordAsync(
                    connection, transaction, sourceFileName, hash, DateTimeOffset.UtcNow,
                    cancellationToken).ConfigureAwait(false);

                existingHashes.Add(hash); // guard against duplicates within the same file
                fileResult.InsertedCount++;
            }

            transaction.Commit();
        }
        catch (OperationCanceledException)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            fileResult.Errors.Add(new ImportError(
                job.CsvFilePath,
                ErrorCode.RowInsertFailure,
                $"Insert failed for '{job.CsvFilePath}'; transaction rolled back: {ex.Message}"));
        }
    }

    private string BuildInsertSql(string tableName, string[] columnNames)
    {
        var q = _dialect.QuoteIdentifier;
        var cols = string.Join(", ", columnNames.Select(q).Append(q("created_at")));
        var parms = string.Join(", ", columnNames.Select(c => "@" + c).Append("@created_at"));
        return $"INSERT INTO {q(tableName)} ({cols}) VALUES ({parms})";
    }

    private static string SanitiseColumnName(string rawHeader)
    {
        var name = rawHeader.Trim().Replace(' ', '_').Replace('-', '_');
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (name.Length > 0 && char.IsDigit(name[0]))
            name = "_" + name;
        return string.IsNullOrEmpty(name) ? "column" : name;
    }
}

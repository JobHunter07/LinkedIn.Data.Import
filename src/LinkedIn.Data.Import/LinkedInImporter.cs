using System.Threading.Channels;
using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Features.ZipIngestion;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import;

/// <summary>
/// Top-level orchestrator. Implements <see cref="ILinkedInImporter"/> by wiring
/// together the ZIP ingestion producer and the CSV processing consumer via a
/// bounded <see cref="Channel{T}"/>.
/// </summary>
public sealed class LinkedInImporter : ILinkedInImporter
{
    private readonly IngestZipsUseCase _ingestZips;
    private readonly ICsvFileImporter _csvImporter;
    private readonly ITableBootstrapper _tableBootstrapper;
    private readonly IImportLogBootstrapper _logBootstrapper;
    private readonly IEventDispatcher _events;
    private readonly Func<System.Data.IDbConnection> _connectionFactory;

    /// <summary>Initialises the orchestrator with its collaborators.</summary>
    public LinkedInImporter(
        IngestZipsUseCase ingestZips,
        ICsvFileImporter csvImporter,
        ITableBootstrapper tableBootstrapper,
        IImportLogBootstrapper logBootstrapper,
        IEventDispatcher events,
        Func<System.Data.IDbConnection> connectionFactory)
    {
        _ingestZips = ingestZips;
        _csvImporter = csvImporter;
        _tableBootstrapper = tableBootstrapper;
        _logBootstrapper = logBootstrapper;
        _events = events;
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc/>
    public async Task<ImportResult> ImportAsync(
        ImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var importResult = new ImportResult();

        // Accumulate per-file results as FileImportCompletedEvents arrive.
        _events.Register<FileImportCompletedEvent>((evt, _) =>
        {
            importResult.FileResults.Add(evt.Result);
            importResult.TotalInserted += evt.Result.InsertedCount;
            importResult.TotalSkipped += evt.Result.SkippedCount;
            importResult.Errors.AddRange(evt.Result.Errors);
            return Task.CompletedTask;
        });

        // Open connection and bootstrap infrastructure tables.
        var connection = _connectionFactory();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        try
        {
            await _logBootstrapper.EnsureCreatedAsync(connection, cancellationToken)
                .ConfigureAwait(false);

            // Create a bounded channel (cap 128, single writer, multi-reader-capable).
            var channelOptions = new BoundedChannelOptions(128)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
            };
            var channel = Channel.CreateBounded<CsvProcessingJob>(channelOptions);

            // One shared temp directory for all archives in this run.
            // Keeping it alive until Task.WhenAll ensures CSV files are still
            // accessible while the consumer processes them concurrently.
            using var tempScope = new TempDirectoryScope();

            // Run producer (ZIP ingestion) and consumer (CSV import) concurrently.
            var producerTask = _ingestZips.RunAsync(
                options.ZipRootDirectory, tempScope.DirectoryPath, channel.Writer, cancellationToken);

            var consumerTask = _csvImporter.RunAsync(
                connection, channel.Reader, cancellationToken);

            // Wait for both to finish before the using-block disposes tempScope.
            await Task.WhenAll(producerTask, consumerTask).ConfigureAwait(false);

            // Collect pre-flight errors from the producer (e.g. missing directory).
            var producerErrors = await producerTask.ConfigureAwait(false);
            foreach (var err in producerErrors)
                importResult.Errors.Add(err);
        }
        finally
        {
            // Do NOT dispose the connection — the factory / caller owns its lifetime.
        }

        // Publish final session event (optional — consumed by external subscribers).
        await _events.PublishAsync(
            new ImportSessionCompletedEvent(importResult), cancellationToken)
            .ConfigureAwait(false);

        return importResult;
    }
}

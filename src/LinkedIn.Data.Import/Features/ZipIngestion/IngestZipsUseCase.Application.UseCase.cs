using System.Threading.Channels;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// Orchestrates ZIP discovery, extraction, and channel-dispatch for all
/// LinkedIn export archives in the configured root directory.
/// </summary>
public sealed class IngestZipsUseCase
{
    private readonly IZipDiscovery _discovery;
    private readonly IZipExtractor _extractor;
    private readonly IEventDispatcher _events;

    /// <summary>Initialises the use case with its required collaborators.</summary>
    public IngestZipsUseCase(
        IZipDiscovery discovery,
        IZipExtractor extractor,
        IEventDispatcher events)
    {
        _discovery = discovery;
        _extractor = extractor;
        _events = events;
    }

    /// <summary>
    /// Discovers and extracts all archives in <paramref name="rootDirectory"/>,
    /// writing one <see cref="CsvProcessingJob"/> per extracted CSV into
    /// <paramref name="channelWriter"/> and completing the writer when done.
    /// 
    /// All archives are extracted into unique subdirectories under 
    /// <paramref name="extractionRoot"/>, whose lifetime is owned by the caller.
    /// </summary>
    /// <returns>
    /// Import errors collected during this phase (e.g. corrupt archives).
    /// Pre-flight failures (missing directory, no archives) are returned without
    /// publishing any events.
    /// </returns>
    public async Task<List<ImportError>> RunAsync(
        string rootDirectory,
        string extractionRoot,
        ChannelWriter<CsvProcessingJob> channelWriter,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ImportError>();

        var discoveryResult = _discovery.Discover(rootDirectory);

        if (!discoveryResult.IsSuccess)
        {
            channelWriter.Complete();
            errors.Add(new ImportError(rootDirectory, discoveryResult.ErrorCode, discoveryResult.ErrorMessage));
            return errors;
        }

        var discovery = discoveryResult.Value;

        // Non-fatal: only one archive type present — record a warning and continue.
        if (!discovery.HasBothTypes)
        {
            errors.Add(new ImportError(
                rootDirectory,
                ErrorCode.SingleArchiveTypeOnly,
                "Only one LinkedIn archive type (Basic or Complete) was found; the other is absent."));
        }

        foreach (var archive in discovery.Archives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractResult = _extractor.Extract(archive, extractionRoot);

            if (!extractResult.IsSuccess)
            {
                errors.Add(new ImportError(
                    Path.GetFileName(archive.FilePath),
                    extractResult.ErrorCode,
                    extractResult.ErrorMessage));
                continue;
            }

            var csvPaths = extractResult.Value
                .Where(p => p.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Publish event BEFORE writing to channel (subscribers may want to log paths).
            await _events.PublishAsync(
                new ArchiveExtractedEvent(Path.GetFileName(archive.FilePath), csvPaths),
                cancellationToken).ConfigureAwait(false);

            foreach (var csvPath in csvPaths)
            {
                var job = new CsvProcessingJob(
                    csvPath,
                    Path.GetFileName(archive.FilePath),
                    archive.ArchiveType);

                await channelWriter.WriteAsync(job, cancellationToken).ConfigureAwait(false);
            }
        }

        channelWriter.Complete();
        return errors;
    }
}

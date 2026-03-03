using System.Threading.Channels;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.IncrementalImport;

/// <summary>
/// Reads <see cref="CsvProcessingJob"/> items from a channel and imports each
/// CSV file incrementally (skipping already-imported rows).
/// </summary>
public interface ICsvFileImporter
{
    /// <summary>
    /// Processes all jobs from <paramref name="channelReader"/> until the channel
    /// is completed. Publishes <see cref="FileImportCompletedEvent"/> after each file.
    /// </summary>
    Task RunAsync(
        System.Data.IDbConnection connection,
        ChannelReader<CsvProcessingJob> channelReader,
        CancellationToken cancellationToken = default);
}

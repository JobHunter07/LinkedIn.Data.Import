namespace LinkedIn.Data.Import.Features.ZipIngestion;

/// <summary>
/// The value returned by <see cref="IZipDiscovery.Discover"/> on success.
/// </summary>
/// <param name="Archives">All discovered archives to process.</param>
/// <param name="HasBothTypes">
/// <see langword="true"/> when both Basic and Complete archives were found.
/// <see langword="false"/> when only one type is present — the caller should
/// record a <see cref="Shared.ErrorCode.SingleArchiveTypeOnly"/> warning.
/// </param>
public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveredArchive> Archives,
    bool HasBothTypes);

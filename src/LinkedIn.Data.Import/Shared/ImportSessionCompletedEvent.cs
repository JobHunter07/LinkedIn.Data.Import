namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Published by the orchestrator (<see cref="ILinkedInImporter"/>) after the
/// entire import session has completed.
/// </summary>
/// <param name="Result">The final aggregate import result for the run.</param>
public sealed record ImportSessionCompletedEvent(
    ImportResult Result) : IDomainEvent;

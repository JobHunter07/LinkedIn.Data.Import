namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Published by the table-bootstrapping feature after a table has been
/// confirmed as ready for row insertion (created or already existed).
/// </summary>
/// <param name="TableName">The database table name.</param>
/// <param name="IsNewlyCreated"><see langword="true"/> if the table was created during this run; <see langword="false"/> if it already existed.</param>
public sealed record TableReadyEvent(
    string TableName,
    bool IsNewlyCreated) : IDomainEvent;

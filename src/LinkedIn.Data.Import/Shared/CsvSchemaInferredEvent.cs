namespace LinkedIn.Data.Import.Shared;

/// <summary>
/// Published by the schema-inference feature after a CSV file's schema has
/// been successfully inferred.
/// </summary>
/// <param name="CsvFilePath">Absolute path to the CSV file.</param>
/// <param name="Schema">The inferred schema for the CSV file.</param>
public sealed record CsvSchemaInferredEvent(
    string CsvFilePath,
    InferredSchema Schema) : IDomainEvent;

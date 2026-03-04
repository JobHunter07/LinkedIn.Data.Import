using Dapper;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Creates the database table for a CSV's inferred schema when it does not
/// already exist. Publishes <see cref="TableReadyEvent"/> after DDL completes.
/// </summary>
public sealed class TableBootstrapper : ITableBootstrapper
{
    private readonly ISqlDialect _dialect;
    private readonly ISchemaEvolver _evolver;
    private readonly IEventDispatcher _events;

    /// <summary>Initialises the bootstrapper with its collaborators.</summary>
    public TableBootstrapper(
        ISqlDialect dialect,
        ISchemaEvolver evolver,
        IEventDispatcher events)
    {
        _dialect = dialect;
        _evolver = evolver;
        _events = events;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> EnsureTableAsync(
        System.Data.IDbConnection connection,
        InferredSchema schema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var q = _dialect.QuoteIdentifier;

            // Check existence first.
            var existingColumns = await _dialect
                .GetExistingColumnNamesAsync(connection, schema.TableName)
                .ConfigureAwait(false);

            bool isNewlyCreated = existingColumns.Count == 0;

            if (isNewlyCreated)
            {
                // Build DDL: dialect-appropriate auto-increment id PK + created_at + inferred columns.
                var createdAtType = _dialect.NormalizeSqlType("DATETIMEOFFSET");

                var idColumn = _dialect switch
                {
                    SqliteDialect => $"{q("id")} INTEGER PRIMARY KEY AUTOINCREMENT",
                    SqlServerDialect => $"{q("id")} INT IDENTITY(1,1) PRIMARY KEY",
                    _ => $"{q("id")} BIGINT PRIMARY KEY",
                };

                var createdAtColumn = _dialect switch
                {
                    SqliteDialect => $"{q("created_at")} {createdAtType} NOT NULL DEFAULT CURRENT_TIMESTAMP",
                    SqlServerDialect => $"{q("created_at")} {createdAtType} NOT NULL DEFAULT SYSDATETIMEOFFSET()",
                    _ => $"{q("created_at")} {createdAtType} NOT NULL",
                };

                var parts = new List<string>
                {
                    idColumn,
                    createdAtColumn,
                };

                foreach (var col in schema.Columns)
                {
                    var normalised = _dialect.NormalizeSqlType(col.SqlType);
                    var nullability = _dialect.GetNullabilityConstraint(col.IsNullable);
                    var colDef = string.IsNullOrEmpty(nullability)
                        ? $"{q(col.Name)} {normalised}"
                        : $"{q(col.Name)} {normalised} {nullability}";
                    parts.Add(colDef);
                }

                var columnsDdl = string.Join(", ", parts);
                var ddl = _dialect.CreateTableIfNotExists(schema.TableName, columnsDdl);

                await connection.ExecuteAsync(
                    new CommandDefinition(ddl, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
            else
            {
                // Table exists — evolve schema if needed.
                var evolveResult = await _evolver.EvolveAsync(
                    connection, schema.TableName, schema, cancellationToken)
                    .ConfigureAwait(false);

                if (!evolveResult.IsSuccess)
                    return Result<bool>.Fail(evolveResult.ErrorCode, evolveResult.ErrorMessage);
            }

            await _events.PublishAsync(
                new TableReadyEvent(schema.TableName, isNewlyCreated),
                cancellationToken).ConfigureAwait(false);

            return Result<bool>.Ok(isNewlyCreated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(
                ErrorCode.TableCreationFailure,
                $"Failed to create/update table '{schema.TableName}': {ex.Message}");
        }
    }
}

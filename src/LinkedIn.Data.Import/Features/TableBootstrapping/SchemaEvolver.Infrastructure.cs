using Dapper;
using LinkedIn.Data.Import.Shared;

namespace LinkedIn.Data.Import.Features.TableBootstrapping;

/// <summary>
/// Adds to an existing table any columns present in a CSV schema that are not
/// yet in the database. Never drops or renames columns.
/// </summary>
public sealed class SchemaEvolver : ISchemaEvolver
{
    private readonly ISqlDialect _dialect;

    /// <summary>Initialises the evolver with its SQL dialect.</summary>
    public SchemaEvolver(ISqlDialect dialect) => _dialect = dialect;

    /// <inheritdoc/>
    public async Task<Result> EvolveAsync(
        System.Data.IDbConnection connection,
        string tableName,
        InferredSchema schema,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _dialect
                .GetExistingColumnNamesAsync(connection, tableName)
                .ConfigureAwait(false);

            foreach (var col in schema.Columns)
            {
                if (existing.Contains(col.Name))
                    continue;

                // Sanitise the identifier through the dialect before embedding
                // in DDL to prevent injection (task 4.4, 4.5).
                var quotedName = _dialect.QuoteIdentifier(col.Name);

                // NEVER emit DROP or RENAME — only ADD (task 4.4).
                var ddl = _dialect.AddColumn(tableName, quotedName, col.SqlType, col.IsNullable);

                await connection.ExecuteAsync(
                    new CommandDefinition(ddl, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(
                ErrorCode.TableCreationFailure,
                $"Schema evolution for table '{tableName}' failed: {ex.Message}");
        }
    }
}

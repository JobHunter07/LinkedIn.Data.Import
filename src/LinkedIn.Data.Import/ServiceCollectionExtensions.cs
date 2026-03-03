using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.IncrementalImport;
using LinkedIn.Data.Import.Features.SchemaInference;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Features.ZipIngestion;
using LinkedIn.Data.Import.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LinkedIn.Data.Import;

/// <summary>
/// Extension methods for registering the LinkedIn Data Import library with
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all LinkedIn Data Import services including
    /// <see cref="ILinkedInImporter"/> and its dependencies.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionFactory">
    /// Factory that creates a new, open-capable <see cref="System.Data.IDbConnection"/>.
    /// Called once per <see cref="ILinkedInImporter.ImportAsync"/> invocation.
    /// </param>
    /// <param name="dialectFactory">
    /// Factory for the SQL dialect to use. Defaults to <see cref="SqliteDialect"/>
    /// if not specified.
    /// </param>
    public static IServiceCollection AddLinkedInImporter(
        this IServiceCollection services,
        Func<IServiceProvider, System.Data.IDbConnection> connectionFactory,
        Func<IServiceProvider, ISqlDialect>? dialectFactory = null)
    {
        // Dialect (default: SQLite)
        services.AddSingleton<ISqlDialect>(sp =>
            dialectFactory?.Invoke(sp) ?? new SqliteDialect());

        // Event dispatcher — scoped per import run.
        services.AddTransient<IEventDispatcher, InProcessEventDispatcher>();

        // ZIP Ingestion
        services.AddTransient<IZipDiscovery, ZipDiscovery>();
        services.AddTransient<IZipExtractor, ZipExtractor>();
        services.AddTransient<IngestZipsUseCase>();

        // Schema Inference
        services.AddTransient<TypeDetector>();
        services.AddTransient<TableNameDeriver>();
        services.AddTransient<ICsvSchemaInferrer, CsvSchemaInferrer>();

        // Table Bootstrapping
        services.AddTransient<IImportLogBootstrapper, ImportLogBootstrapper>();
        services.AddTransient<ISchemaEvolver, SchemaEvolver>();
        services.AddTransient<ITableBootstrapper, TableBootstrapper>();

        // Import Tracking
        services.AddTransient<IRowHasher, RowHasher>();
        services.AddTransient<IImportLogRepository, ImportLogRepository>();

        // Incremental Import
        services.AddTransient<ICsvFileImporter, CsvFileImporter>();

        // Orchestrator
        services.AddTransient<ILinkedInImporter>(sp =>
        {
            var events = sp.GetRequiredService<IEventDispatcher>();

            // Wire up cross-feature event subscriptions.
            var bootstrapper = sp.GetRequiredService<ITableBootstrapper>();
            var connection = connectionFactory(sp);
            events.Register<CsvSchemaInferredEvent>(async (evt, ct) =>
                await bootstrapper.EnsureTableAsync(connection, evt.Schema, ct).ConfigureAwait(false));

            return new LinkedInImporter(
                sp.GetRequiredService<IngestZipsUseCase>(),
                sp.GetRequiredService<ICsvFileImporter>(),
                sp.GetRequiredService<ITableBootstrapper>(),
                sp.GetRequiredService<IImportLogBootstrapper>(),
                events,
                () => connectionFactory(sp));
        });

        return services;
    }
}

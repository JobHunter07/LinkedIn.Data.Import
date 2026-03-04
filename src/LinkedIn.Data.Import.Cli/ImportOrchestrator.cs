using LinkedIn.Data.Import;
using LinkedIn.Data.Import.Features.ImportTracking;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Orchestrates the LinkedIn data import process.
/// Coordinates user interaction, configuration, and execution of the import pipeline.
/// </summary>
/// <remarks>
/// This orchestrator follows the Orchestrator Pattern:
/// - Manages the overall workflow and coordination
/// - Delegates LinkedIn-specific processing to ILinkedInImporter (Pipeline Pattern)
/// - Manages logging, error handling, and exit codes
/// </remarks>
internal sealed class ImportOrchestrator
{
    /// <summary>
    /// Runs the LinkedIn import with pre-configured settings.
    /// </summary>
    /// <param name="options">Pre-configured import options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code: 0 for success, non-zero for failure</returns>
    public async Task<int> RunImportAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            MainMenu.ShowBanner();

            // Display what we're importing
            AnsiConsole.Write(
                new Rule("[bold blue]LinkedIn Import[/]")
                    .RuleStyle("blue")
                    .LeftJustified());
            AnsiConsole.WriteLine();

            var infoTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("")
                .AddColumn("");

            infoTable.AddRow(
                "[bold]Path to directory containing LinkedIn ZIP exports:[/]",
                $"[cyan]{Markup.Escape(options.ZipRootDirectory)}[/]");

            infoTable.AddRow(
                "[bold]SQL Server connection string:[/]",
                $"[cyan]{MaskConnectionString(options.ConnectionString)}[/]");

            AnsiConsole.Write(infoTable);
            AnsiConsole.WriteLine();

            // Build service provider with user-provided connection string
            using var serviceProvider = BuildServiceProvider(options.ConnectionString);

            // Execute the import
            var result = await ExecuteImportAsync(serviceProvider, options, cancellationToken);

            // Report results
            await ReportResultsAsync(serviceProvider, result, cancellationToken);

            return result.IsSuccess ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Import cancelled by user.[/]");
            return 130; // Standard cancellation exit code
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Unexpected error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    /// <summary>
    /// Builds the service provider with the specified connection string.
    /// </summary>
    private static ServiceProvider BuildServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();

        // Register connection factory with user-provided connection string
        Func<System.Data.IDbConnection> connectionFactory = () => new SqlConnection(connectionString);
        services.AddSingleton(connectionFactory);

        // Register LinkedIn importer and all its dependencies
        services.AddLinkedInImporter(
            connectionFactory: _ => connectionFactory(),
            dialectFactory: _ => new SqlServerDialect());

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Executes the import using the LinkedIn importer from the library.
    /// </summary>
    private static async Task<ImportResult> ExecuteImportAsync(
        ServiceProvider serviceProvider,
        ImportOptions options,
        CancellationToken cancellationToken)
    {
        var importer = serviceProvider.GetRequiredService<ILinkedInImporter>();
        var dispatcher = serviceProvider.GetRequiredService<IEventDispatcher>();

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("blue bold"))
            .StartAsync("[blue]Connecting and bootstrapping schema…[/]", async ctx =>
            {
                // Subscribe to file completion events to update progress
                dispatcher.Register<FileImportCompletedEvent>((evt, _) =>
                {
                    ctx.Status = $"Processed [cyan]{Markup.Escape(Path.GetFileName(evt.SourceFile))}[/]";
                    return Task.CompletedTask;
                });

                return await importer.ImportAsync(options, cancellationToken);
            });
    }

    /// <summary>
    /// Reports the import results to the console.
    /// </summary>
    private static async Task ReportResultsAsync(
        ServiceProvider serviceProvider,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        // Use existing ResultsRenderer for comprehensive output
        ResultsRenderer.Render(result);

        // Show detailed skip report if applicable
        var importLog = serviceProvider.GetRequiredService<IImportLogRepository>();
        var connectionFactory = serviceProvider.GetRequiredService<Func<System.Data.IDbConnection>>();

        await SkipReportRenderer.RenderAsync(
            result,
            importLog,
            connectionFactory,
            cancellationToken);
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            var parts = connectionString.Split(';');
            return string.Join(";", parts.Select(part =>
            {
                if (part.Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                {
                    return "Password=********";
                }
                return part;
            }));
        }

        return connectionString;
    }
}

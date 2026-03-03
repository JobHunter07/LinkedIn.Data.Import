using LinkedIn.Data.Import.Shared;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace LinkedIn.Data.Import.Cli;

/// <summary>
/// Hosted service that drives a single LinkedIn import run:
/// subscribes to domain events for live progress, calls ImportAsync,
/// renders results, sets the process exit code, and stops the host.
/// </summary>
internal sealed class ImportHostedService : IHostedService
{
    private readonly ILinkedInImporter _importer;
    private readonly IEventDispatcher _dispatcher;
    private readonly ImportOptions _options;
    private readonly IHostApplicationLifetime _lifetime;

    public ImportHostedService(
        ILinkedInImporter importer,
        IEventDispatcher dispatcher,
        ImportOptions options,
        IHostApplicationLifetime lifetime)
    {
        _importer = importer;
        _dispatcher = dispatcher;
        _options = options;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ImportResult result = null!;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("blue bold"))
            .StartAsync("[blue]Connecting and bootstrapping schema…[/]", async ctx =>
            {
                // ── Subscribe for per-file progress BEFORE calling ImportAsync ──
                // Tasks 4.1 & 4.2: update the spinner status text on each completed file.
                _dispatcher.Register<FileImportCompletedEvent>((evt, _) =>
                {
                    ctx.Status =
                        $"Processed [cyan]{Markup.Escape(Path.GetFileName(evt.SourceFile))}[/]";
                    return Task.CompletedTask;
                });

                result = await _importer.ImportAsync(_options, cancellationToken);
            });

        // ── Phase 3: Render the results summary ──────────────────────────────
        ResultsRenderer.Render(result);

        // ── Task 6.1: Set exit code and stop the host ─────────────────────────
        Environment.ExitCode = result.IsSuccess ? 0 : 1;
        _lifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

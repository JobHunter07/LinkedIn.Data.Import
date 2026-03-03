## Context

The `LinkedIn.Data.Import` library already provides a complete, tested import pipeline. Consuming it currently requires writing a host application that wires up DI, configures `ImportOptions`, and interprets `ImportResult`. A CLI project eliminates this boilerplate and makes the library directly executable by developers and power users.

The existing `src/LinkedIn.Data.Import.Cli/` directory is already stubbed in the solution but contains no meaningful code.

## Goals / Non-Goals

**Goals:**
- Create a fully functional, interactive console application that drives the library through its public API.
- Use `Spectre.Console` for all output and interactive prompts — no raw `Console.Write`.
- Collect `ZipRootDirectory` and SQL Server connection string from the user via typed/selection prompts.
- Display live per-file progress during the import (spinner or progress bar).
- Render a structured results summary (table of file results, totals, errors panel) after completion.
- Wire DI inside a minimal `Host` using `Microsoft.Extensions.Hosting`.

**Non-Goals:**
- Persistent configuration storage (no `.config` file or user secrets reads).
- Unattended / CI-mode (no `--no-prompt` flag in this change).
- Database connection testing before running the import.
- Packaging or publishing the CLI as a dotnet tool.

## Decisions

### D1 — Use `Microsoft.Extensions.Hosting` for the host

**Decision**: Use `Host.CreateDefaultBuilder` / `IHostedService` pattern rather than a hand-rolled DI container.

**Rationale**: `AddLinkedInImporter` already targets `IServiceCollection`. Using the generic host picks up logging, configuration, and DI for free with zero extra plumbing. The `IHostedService` exits cleanly after a single import run.

**Alternative considered**: Plain `ServiceCollection` + `BuildServiceProvider` — simpler but loses structured logging and clean shutdown semantics.

---

### D2 — Spectre.Console for all UI

**Decision**: Add `Spectre.Console` as the sole UI framework. No direct `Console.*` calls in application code.

**Rationale**: The user explicitly requires Spectre.Console. It provides `TextPrompt<string>`, `SelectionPrompt<T>`, `Progress` / `Status` spinners, `Table`, and `Panel` — covering every UI need of the wizard and results display. Its markup escaping (`Markup.Escape`) keeps error messages safe.

**Alternative considered**: `System.CommandLine` with `Spectre.Console` on top — added complexity without benefit for a single-command tool.

---

### D3 — Two-phase UI: wizard → import → results

**Decision**: Split the run into three sequential phases:
1. **Wizard** — collect `ZipRootDirectory` and `ConnectionString` via prompts.
2. **Import** — call `ILinkedInImporter.ImportAsync`, wrapped in a `Spectre.Console Status` spinner. Relay domain events to update the spinner status text with the current file name.
3. **Results** — render a `Table` of per-file results and an error `Panel` if errors exist.

**Rationale**: Clean separation keeps each phase testable and the code readable. The `InProcessEventDispatcher` already fires `FileImportCompletedEvent`; subscribing to events provides natural per-file progress hooks without coupling the CLI to library internals.

---

### D4 — Connection string collected as a plain `TextPrompt`, not interactive builder

**Decision**: Ask for the full ADO.NET connection string as a single text prompt (with a default `Server=.;Database=LinkedInData;Trusted_Connection=True;` hint).

**Rationale**: Building a multi-field connection string wizard is scope creep. Advanced users can paste their string; beginners see the default and press Enter. A `SecretPrompt` is not needed because SQL Server connection strings are not secrets in a developer-tool context.

## Risks / Trade-offs

- **Long connection strings are opaque** — users must know the ADO.NET format. Mitigation: provide a clear default and a short hint in the prompt label.
- **No validation of ZipRootDirectory before running** — invalid paths surface as import errors rather than a friendly pre-flight message. Mitigation: add a basic `Directory.Exists` check with `ValidationResult` on the `TextPrompt`.
- **Spectre.Console Progress and async** — `Progress.StartAsync` wraps the async import correctly; must ensure `await` is propagated so exceptions are not swallowed.
- **Event subscription timing** — events must be subscribed before `ImportAsync` is called; ordering is straightforward given DI but must be documented in tasks.

## Migration Plan

1. Add `LinkedIn.Data.Import.Cli.csproj` targeting `net10.0` with project reference to `LinkedIn.Data.Import`.
2. Add `Spectre.Console` and `Microsoft.Extensions.Hosting` NuGet refs.
3. Implement `Program.cs` with host builder and `ImportHostedService`.
4. No database or schema changes; no changes to existing projects.
5. Rollback: remove the CLI project from the solution — no other project depends on it.

## Open Questions

- None — scope is well-defined by the proposal and the library's existing public API.

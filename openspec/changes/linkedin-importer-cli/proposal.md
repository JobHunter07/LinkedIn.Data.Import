## Why

Developers and power users need a standalone executable entry point for the LinkedIn.Data.Import library that doesn't require writing custom host code. A rich, interactive CLI removes friction for one-off imports and makes the library immediately usable without a bespoke integration.

## What Changes

- Add a new `LinkedIn.Data.Import.Cli` console project to the solution targeting .NET 10.
- Reference `LinkedIn.Data.Import` as a project dependency.
- Add `Spectre.Console` as an explicit NuGet dependency to drive all terminal output and interactive prompts.
- Implement an interactive wizard that collects `ImportOptions` from the user (ZIP root directory, SQL Server connection string), then runs the import and displays results.
- Wire up dependency injection (`AddLinkedInImporter`) inside the CLI host.
- Present import progress (per-file spinner/progress), a summary table of inserted/skipped rows, and any errors in a formatted panel.

## Capabilities

### New Capabilities

- `cli-console-app`: Interactive Spectre.Console CLI that wraps the LinkedIn.Data.Import library — collects user inputs via typed prompts, runs the import with live progress display, and renders a rich results summary.

### Modified Capabilities

<!-- none -->

## Impact

- New project: `src/LinkedIn.Data.Import.Cli/LinkedIn.Data.Import.Cli.csproj`
- New NuGet dependency: `Spectre.Console` (latest stable) in the CLI project only.
- Solution file `LinkedIn.Data.Import.slnx` gains the new project reference.
- No changes to the existing `LinkedIn.Data.Import` library or its public API.
- No changes to existing tests.

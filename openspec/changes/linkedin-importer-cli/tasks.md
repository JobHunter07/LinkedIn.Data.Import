## 1. Project Scaffolding

- [x] 1.1 Create `src/LinkedIn.Data.Import.Cli/LinkedIn.Data.Import.Cli.csproj` targeting `net10.0` as an `Exe` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- [x] 1.2 Add `<ProjectReference>` to `../LinkedIn.Data.Import/LinkedIn.Data.Import.csproj`
- [x] 1.3 Add `<PackageReference Include="Spectre.Console" Version="0.*" />` to the CLI project
- [x] 1.4 Add `<PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />` to the CLI project
- [x] 1.5 Add the CLI project to `LinkedIn.Data.Import.slnx`

## 2. Host & DI Wiring

- [x] 2.1 Create `Program.cs` — parse collected options, build the generic host with `Host.CreateDefaultBuilder`, register `AddLinkedInImporter` using a `SqlConnection` factory from the connection string, register `ImportHostedService`, and run the host
- [x] 2.2 Create `ImportHostedService.cs` implementing `IHostedService` — accepts `ILinkedInImporter`, `IHostApplicationLifetime`, and `IEventDispatcher` via constructor injection; stores collected `ImportOptions`; calls `ImportAsync` in `StartAsync`; stops the host on completion

## 3. Interactive Wizard

- [x] 3.1 Create `Wizard.cs` (or a static helper) — use `new TextPrompt<string>("ZIP root directory:")` with `.Validate(path => Directory.Exists(path) ? ValidationResult.Success() : ValidationResult.Error("[red]Directory not found[/]"))` to collect `ZipRootDirectory`
- [x] 3.2 Add a `TextPrompt<string>` for the connection string with `.DefaultValue("Server=.;Database=LinkedInData;Trusted_Connection=True;TrustServerCertificate=True;")`
- [x] 3.3 Return a populated `ImportOptions` record/object from the wizard for use in host construction

## 4. Live Progress Display

- [x] 4.1 Subscribe to `FileImportCompletedEvent` via `IEventDispatcher` before calling `ImportAsync`, storing the latest file name in a shared field
- [x] 4.2 Wrap `ImportAsync` in `AnsiConsole.Status().StartAsync(...)` — update the spinner `ctx.Status` text with the current file name on each event

## 5. Results Summary Rendering

- [x] 5.1 Create a `ResultsRenderer.cs` (or inline method) — build a `Spectre.Console Table` with columns `File`, `Inserted`, `Skipped` and populate one row per `FileImportResult`
- [x] 5.2 Add a totals row to the table displaying `result.TotalInserted` and `result.TotalSkipped` in bold
- [x] 5.3 If `result.Errors` is non-empty, render a `Panel` containing each `ImportError` formatted as `[{err.Code}] {Markup.Escape(err.SourceFile)}: {Markup.Escape(err.Message)}`
- [x] 5.4 Write the table and optional error panel to `AnsiConsole` after the import completes

## 6. Exit Code

- [x] 6.1 In `ImportHostedService`, after rendering results, call `Environment.ExitCode = result.IsSuccess ? 0 : 1` before stopping the host via `IHostApplicationLifetime.StopApplication()`

## 7. Verification

- [x] 7.1 Run `dotnet build` on the solution and confirm zero errors
- [ ] 7.2 Run the CLI against a real LinkedIn export ZIP directory and confirm the wizard prompts, spinner runs, and results table renders correctly
- [ ] 7.3 Run the CLI with an invalid directory and confirm the validation error re-prompts without crashing

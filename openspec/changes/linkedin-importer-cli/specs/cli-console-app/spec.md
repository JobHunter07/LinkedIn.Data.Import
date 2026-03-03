## ADDED Requirements

### Requirement: CLI project exists and builds
The solution SHALL contain a `LinkedIn.Data.Import.Cli` console project targeting `net10.0` that compiles without errors and references `LinkedIn.Data.Import` as a project dependency.

#### Scenario: Project builds successfully
- **WHEN** the developer runs `dotnet build` on the solution
- **THEN** the `LinkedIn.Data.Import.Cli` binary is produced with exit code 0

---

### Requirement: Spectre.Console is the sole UI framework
The CLI project SHALL list `Spectre.Console` as an explicit `<PackageReference>` in its `.csproj`. No raw `Console.Write` / `Console.Read` calls SHALL appear in application code.

#### Scenario: Spectre.Console package reference present
- **WHEN** the project file is inspected
- **THEN** a `<PackageReference Include="Spectre.Console" ...>` element is present

---

### Requirement: Interactive wizard collects import options
On startup the CLI SHALL display an interactive wizard using Spectre.Console prompts that collects:
1. **ZIP root directory** — a `TextPrompt<string>` with a `ValidationResult` check that the directory exists.
2. **SQL Server connection string** — a `TextPrompt<string>` with a sensible default value pre-filled.

#### Scenario: Valid directory accepted
- **WHEN** the user enters a path to a directory that exists
- **THEN** the wizard advances to the connection string prompt

#### Scenario: Non-existent directory rejected
- **WHEN** the user enters a path that does not exist on disk
- **THEN** the wizard displays a validation error and re-prompts for the ZIP root directory

#### Scenario: Connection string default accepted
- **WHEN** the user presses Enter without typing a connection string
- **THEN** the wizard uses the pre-filled default connection string value

---

### Requirement: Import runs with live progress display
After the wizard completes, the CLI SHALL call `ILinkedInImporter.ImportAsync` inside a `Spectre.Console Status` spinner. The spinner status text SHALL update to show the name of the CSV file currently being processed (via domain event subscription).

#### Scenario: Spinner visible during import
- **WHEN** the import is running
- **THEN** a Spectre.Console spinner is visible in the terminal with a non-empty status message

#### Scenario: Per-file status updates
- **WHEN** a `FileImportCompletedEvent` fires during the import
- **THEN** the spinner status text reflects the completed file's name

---

### Requirement: Results summary rendered after import
After `ImportAsync` returns, the CLI SHALL render:
- A `Spectre.Console Table` with one row per `FileImportResult` showing: file name, rows inserted, rows skipped.
- A totals row at the bottom of the table showing `TotalInserted` and `TotalSkipped`.
- If `result.Errors` is non-empty, an error `Panel` listing each `ImportError` (code, source file, message).

#### Scenario: Successful import shows table
- **WHEN** the import completes with no errors
- **THEN** a table of file results is rendered and no error panel is displayed

#### Scenario: Import with errors shows error panel
- **WHEN** the import completes with one or more errors
- **THEN** the results table is rendered AND an error panel listing each error is displayed

#### Scenario: Non-fatal warning does not suppress results table
- **WHEN** the import result contains only non-fatal warnings (e.g., `SingleArchiveTypeOnly`)
- **THEN** the results table is still rendered and the error panel shows the warning

---

### Requirement: Process exit code reflects import outcome
The CLI process SHALL exit with code `0` when `ImportResult.IsSuccess` is `true`, and with a non-zero code (e.g., `1`) when `IsSuccess` is `false`.

#### Scenario: Successful import exits zero
- **WHEN** all files import without errors
- **THEN** the process exits with code 0

#### Scenario: Failed import exits non-zero
- **WHEN** at least one fatal error is recorded in `ImportResult.Errors`
- **THEN** the process exits with a non-zero exit code

---

### Requirement: Dependency injection wired via IHostedService
The CLI SHALL use `Microsoft.Extensions.Hosting` (`Host.CreateDefaultBuilder`) to bootstrap DI. `AddLinkedInImporter` SHALL be called on the `IServiceCollection` with a `SqlConnection` factory derived from the collected connection string. The import logic SHALL run inside an `IHostedService` that stops the host on completion.

#### Scenario: DI container resolves ILinkedInImporter
- **WHEN** the host is built with the collected connection string
- **THEN** `ILinkedInImporter` resolves from the DI container without error

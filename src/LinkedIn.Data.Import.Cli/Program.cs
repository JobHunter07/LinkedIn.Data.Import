using LinkedIn.Data.Import.Cli.Pipeline;
using LinkedIn.Data.Import.Cli.Pipeline.Steps;
using Microsoft.Extensions.Configuration;

// ────────────────────────────────────────────────────────────────────────────
// Program.cs: Entry point - wires up the pipeline and runs it
// ────────────────────────────────────────────────────────────────────────────

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Build the import pipeline
var pipeline = new ImportPipeline()
    .AddStep(new ConfigurationStep(config))
    .AddStep(new ExtractionStep())
    .AddStep(new DeduplicationStep())
    .AddStep(new ImportStep());

// Execute the pipeline
var context = new ImportContext();
var result = await pipeline.ExecuteAsync(context);

// Return exit code
return result.ExitCode;

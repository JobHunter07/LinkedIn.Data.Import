using LinkedIn.Data.Import.Cli;
using Microsoft.Extensions.Configuration;

// ────────────────────────────────────────────────────────────────────────────
// Program.cs: Entry point - wires up dependencies and starts the application
// ────────────────────────────────────────────────────────────────────────────

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Create application components
var settingsManager = new SettingsManager(config);
var orchestrator = new ImportOrchestrator();
var controller = new ApplicationController(settingsManager, orchestrator);

// Run the application
return await controller.RunAsync();

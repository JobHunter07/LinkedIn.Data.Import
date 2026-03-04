# Copilot Instructions

## Project Guidelines
- When the user reports errors: first create tests to catch the errors, then write code fixes, then run tests to confirm; add this workflow to the repo's GitHub Copilot instructions.
- On user-reported errors workflow: Tests-first -> Fix -> Run tests. Persist this as the default guidance for Copilot interactions.

## Configuration Management
- Save only the database connection string (Import:ConnectionString) to .NET User Secrets; keep other defaults in project appsettings.json.
- On app startup, show prompts with defaults from User Secrets; the user can press Enter to accept or type to override. If the user chooses to save, persist the connection string into User Secrets.
- Use Microsoft.Extensions.Configuration.UserSecrets and dotnet user-secrets conventions for managing secrets.
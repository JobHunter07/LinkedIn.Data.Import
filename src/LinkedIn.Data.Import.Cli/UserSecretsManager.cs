using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace LinkedIn.Data.Import.Cli;

public static class UserSecretsManager
{
    /// <summary>
    /// Writes a secret key/value pair to the user secrets store for this assembly.
    /// If <paramref name="userSecretsId"/> is provided it will be used instead of the
    /// assembly's UserSecretsId attribute (useful for testing).
    /// </summary>
    public static void WriteSecret(string key, string value, string? userSecretsId = null, string? baseFolder = null)
    {
        if (string.IsNullOrEmpty(userSecretsId))
        {
            var entry = Assembly.GetEntryAssembly();
            var attr = entry?.GetCustomAttribute<UserSecretsIdAttribute>();
            if (attr is null || string.IsNullOrEmpty(attr.UserSecretsId))
                throw new InvalidOperationException("No UserSecretsId found for the entry assembly.");
            userSecretsId = attr.UserSecretsId;
        }

        // Prefer using the official `dotnet user-secrets` tool when possible as
        // it is the supported mechanism and reduces risk of secrets file format
        // issues. Attempt the CLI first and fall back to writing secrets.json.
        try
        {
            var projectPath = FindCandidateProjectPath(userSecretsId);
            if (!string.IsNullOrEmpty(projectPath) && TryWriteWithDotnetCli(key, value, projectPath))
                return;
        }
        catch
        {
            // Swallow — we'll fall back to file write below. Do not leak secrets.
        }

        baseFolder ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var secretsDir = Path.Combine(baseFolder, "Microsoft", "UserSecrets", userSecretsId);
        Directory.CreateDirectory(secretsDir);
        var secretsPath = Path.Combine(secretsDir, "secrets.json");

        Dictionary<string, string?> data = new();
        if (File.Exists(secretsPath))
        {
            var existing = File.ReadAllText(secretsPath);
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, string?>>(existing) ?? new Dictionary<string, string?>();
            }
            catch
            {
                data = new Dictionary<string, string?>();
            }
        }

        data[key] = value;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(secretsPath, json);
    }

    private static string? FindCandidateProjectPath(string? userSecretsId)
    {
        // Search upward from the base directory for a .csproj file. Prefer a
        // project that contains the matching UserSecretsId value when possible.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; depth < 10 && dir != null; depth++)
        {
            var csprojFiles = dir.GetFiles("*.csproj");
            if (csprojFiles.Length > 0)
            {
                if (!string.IsNullOrEmpty(userSecretsId))
                {
                    foreach (var f in csprojFiles)
                    {
                        try
                        {
                            var content = File.ReadAllText(f.FullName);
                            if (content.Contains(userSecretsId, StringComparison.OrdinalIgnoreCase))
                                return f.FullName;
                        }
                        catch
                        {
                            // ignore read errors
                        }
                    }
                }

                // Fall back to first csproj in the directory
                return csprojFiles[0].FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool TryWriteWithDotnetCli(string key, string value, string projectPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"user-secrets set \"{key}\" \"{value}\" --project \"{projectPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return false;

            // Wait a short time; the CLI should be quick for a single set.
            proc.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

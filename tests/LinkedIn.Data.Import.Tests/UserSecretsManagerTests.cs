using System.Text.Json;
using Xunit;
using LinkedIn.Data.Import.Cli;

namespace LinkedIn.Data.Import.Tests;

public class UserSecretsManagerTests
{
    [Fact]
    public void WriteSecret_WritesJsonFile_ToCustomFolder()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmp);
        try
        {
            var id = "test-secrets-id";
            UserSecretsManager.WriteSecret("Import:ConnectionString", "Server=.;Db=X;", id, tmp);

            var secretsPath = Path.Combine(tmp, "Microsoft", "UserSecrets", id, "secrets.json");
            Assert.True(File.Exists(secretsPath));

            var json = File.ReadAllText(secretsPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
            Assert.Equal("Server=.;Db=X;", dict["Import:ConnectionString"]);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }
}

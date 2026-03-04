using Microsoft.Extensions.Configuration;

namespace LinkedIn.Data.Import.Cli;

public static class ImportDefaultsProvider
{
    public static string? GetDefaultConnectionString(IConfiguration config)
    {
        if (config is null)
            return null;

        return config["Import:ConnectionString"];
    }
}

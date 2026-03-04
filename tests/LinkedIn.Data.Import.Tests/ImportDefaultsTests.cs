using Microsoft.Extensions.Configuration;
using LinkedIn.Data.Import.Cli;
using Xunit;

namespace LinkedIn.Data.Import.Tests;

public class ImportDefaultsTests
{
    [Fact]
    public void GetDefaultConnectionString_Returns_ConfigValue()
    {
        var dict = new Dictionary<string, string?> { { "Import:ConnectionString", "Server=.;Database=Test;" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var result = ImportDefaultsProvider.GetDefaultConnectionString(config);

        Assert.Equal("Server=.;Database=Test;", result);
    }
}

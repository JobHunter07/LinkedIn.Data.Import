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

    [Fact]
    public void GetDefaultZipRootDirectory_ReturnsNullWhenConfigIsNull()
    {
        // Act
        var result = ImportDefaultsProvider.GetDefaultZipRootDirectory(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultZipRootDirectory_ReturnsValueFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Import:ZipRootDirectory"] = @"C:\TestPath\LinkedIn"
            })
            .Build();

        // Act
        var result = ImportDefaultsProvider.GetDefaultZipRootDirectory(config);

        // Assert
        Assert.Equal(@"C:\TestPath\LinkedIn", result);
    }

    [Fact]
    public void GetDefaultZipRootDirectory_ReturnsNullWhenNotInConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var result = ImportDefaultsProvider.GetDefaultZipRootDirectory(config);

        // Assert
        Assert.Null(result);
    }
}

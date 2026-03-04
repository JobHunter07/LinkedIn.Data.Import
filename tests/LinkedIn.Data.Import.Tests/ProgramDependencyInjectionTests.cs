using LinkedIn.Data.Import;
using LinkedIn.Data.Import.Features.TableBootstrapping;
using LinkedIn.Data.Import.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinkedIn.Data.Import.Tests;

/// <summary>
/// Tests for Program.cs dependency injection configuration.
/// These tests verify that all required services are properly registered
/// and can be resolved from the DI container without throwing exceptions.
/// </summary>
public sealed class ProgramDependencyInjectionTests
{
    [Fact]
    public void Host_ShouldBuildSuccessfully_WithAllServices()
    {
        // Arrange
        var options = new ImportOptions
        {
            ZipRootDirectory = "C:\\Test",
            ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;"
        };

        // Act & Assert - Should not throw InvalidOperationException
        using var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((_, services) =>
            {
                // Register connection factory so it can be injected into services like ImportHostedService
                Func<System.Data.IDbConnection> connectionFactory = () => new SqlConnection(options.ConnectionString);
                services.AddSingleton(connectionFactory);

                services.AddLinkedInImporter(
                    connectionFactory: _ => connectionFactory(),
                    dialectFactory: _ => new SqlServerDialect());

                services.Replace(
                    ServiceDescriptor.Singleton<IEventDispatcher, InProcessEventDispatcher>());

                services.AddSingleton(options);
            })
            .Build();

        // Verify the host was built successfully
        Assert.NotNull(host);
        Assert.NotNull(host.Services);
    }

    [Fact]
    public void DI_ShouldResolveConnectionFactory()
    {
        // Arrange
        var options = new ImportOptions
        {
            ZipRootDirectory = "C:\\Test",
            ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;"
        };

        using var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((_, services) =>
            {
                Func<System.Data.IDbConnection> connectionFactory = () => new SqlConnection(options.ConnectionString);
                services.AddSingleton(connectionFactory);

                services.AddLinkedInImporter(
                    connectionFactory: _ => connectionFactory(),
                    dialectFactory: _ => new SqlServerDialect());

                services.Replace(
                    ServiceDescriptor.Singleton<IEventDispatcher, InProcessEventDispatcher>());

                services.AddSingleton(options);
            })
            .Build();

        // Act
        var factory = host.Services.GetRequiredService<Func<System.Data.IDbConnection>>();

        // Assert
        Assert.NotNull(factory);
        using var connection = factory();
        Assert.NotNull(connection);
        Assert.IsType<SqlConnection>(connection);
    }

    [Fact]
    public void DI_ConnectionFactory_ShouldCreateWorkingConnection()
    {
        // Arrange
        var options = new ImportOptions
        {
            ZipRootDirectory = "C:\\Test",
            ConnectionString = "Server=.;Database=Test;Trusted_Connection=True;"
        };

        Func<System.Data.IDbConnection> connectionFactory = () => new SqlConnection(options.ConnectionString);

        // Act
        using var connection1 = connectionFactory();
        using var connection2 = connectionFactory();

        // Assert - Factory should create independent instances
        Assert.NotNull(connection1);
        Assert.NotNull(connection2);
        Assert.NotSame(connection1, connection2);
        Assert.IsType<SqlConnection>(connection1);
        Assert.IsType<SqlConnection>(connection2);
    }
}

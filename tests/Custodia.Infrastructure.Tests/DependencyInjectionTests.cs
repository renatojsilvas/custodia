using Custodia.Infrastructure.Observability;
using Custodia.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Custodia.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    private static IConfiguration BuildConfiguration(
        string host = "localhost",
        int port = 5432,
        string database = "custodia_teste",
        string username = "custodia_app",
        string password = "segredo") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    $"Host={host};Port={port};Database={database};Username={username};Password={password}",
            })
            .Build();

    [Fact]
    public void AddInfrastructure_RegistraAppDbContextENpgsqlDataSource_ETodosResolvem()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.NotNull(dataSource);
        Assert.NotNull(dbContext);
    }

    [Fact]
    public void AddInfrastructure_NpgsqlDataSource_EhSingleton()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<NpgsqlDataSource>();
        using var scope = provider.CreateScope();
        var second = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddInfrastructure_ConnectionStringDoDataSource_TemNoResetOnCloseEMaxPoolSizeDoPadrao()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();

        var builder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        Assert.True(
            builder.NoResetOnClose,
            "NoResetOnClose evita o RESET a cada devolução de conexão à pool.");

        Assert.Equal(5, builder.MaxPoolSize);
    }

    [Fact]
    public void AddInfrastructure_ConnectionStringDoDataSource_PreservaHostPortaDatabaseECredencialDaConfiguracaoDeEntrada()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration(
            host: "db.interno", port: 6543, database: "custodia", username: "custodia_role", password: "s3nha"));

        using var provider = services.BuildServiceProvider();
        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();

        var builder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        Assert.Equal("db.interno", builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("custodia", builder.Database);
        Assert.Equal("custodia_role", builder.Username);

        Assert.Null(builder.Password);
    }

    [Fact]
    public void AddInfrastructure_RegistraIApiKeyMetricsComoApiKeyMetrics()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var metrics = provider.GetRequiredService<IApiKeyMetrics>();

        Assert.IsType<ApiKeyMetrics>(metrics);
    }

    [Fact]
    public void AddInfrastructure_RegistraIApiKeyMetricsComoSingleton()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IApiKeyMetrics>();
        using var scope = provider.CreateScope();
        var second = scope.ServiceProvider.GetRequiredService<IApiKeyMetrics>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddInfrastructure_RegistraTimeProviderDoSistemaComoSingleton()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<TimeProvider>();
        using var scope = provider.CreateScope();
        var second = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        Assert.Same(TimeProvider.System, first);
        Assert.Same(first, second);
    }
}

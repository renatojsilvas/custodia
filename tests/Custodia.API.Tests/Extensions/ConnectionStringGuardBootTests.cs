using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Custodia.API.Tests.Extensions;

public sealed class ConnectionStringGuardBootTests
{
    [Fact]
    public void Boot_Production_WithConnectionStringMissingCredentials_ShouldFailToStartWithMessageContainingConnectionStringName()
    {
        using var factory = new ProductionWithoutConnectionCredentialFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Testing_WithConnectionStringMissingCredentials_ShouldNotFailToStart()
    {
        using var factory = new TestingWithoutConnectionCredentialFactory();

        _ = factory.Services;
    }

    private sealed class ProductionWithoutConnectionCredentialFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Timeout=2",
                    ["ApiKey:Key"] = new string('a', 64)
                });
            });
        }
    }

    private sealed class TestingWithoutConnectionCredentialFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Timeout=2",
                    ["ApiKey:Key"] = ""
                });
            });
        }
    }
}

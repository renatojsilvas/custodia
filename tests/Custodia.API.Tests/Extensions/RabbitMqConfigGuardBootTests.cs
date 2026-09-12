using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Custodia.API.Tests.Extensions;

public sealed class RabbitMqConfigGuardBootTests
{
    [Fact]
    public void Boot_Production_SemRabbitMqHost_FalhaComMensagemContendoAChaveDeConfiguracao()
    {
        using var factory = new ProductionWithoutRabbitMqFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("RabbitMq:Host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Testing_SemRabbitMqHost_NaoFalhaAoSubir()
    {
        using var factory = new TestingWithoutRabbitMqFactory();

        _ = factory.Services;
    }

    private sealed class ProductionWithoutRabbitMqFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=fake;Username=fake;Password=fake",
                    ["ApiKey:Key"] = new string('a', 64),
                });
            });
        }
    }

    private sealed class TestingWithoutRabbitMqFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Timeout=2",
                    ["ApiKey:Key"] = "",
                });
            });
        }
    }
}

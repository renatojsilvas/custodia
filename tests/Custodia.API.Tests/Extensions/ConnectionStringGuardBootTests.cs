using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Custodia.API.Tests.Extensions;

public sealed class ConnectionStringGuardBootTests
{
    [Fact]
    public void Boot_Production_WithGuardEnabled_ShouldFailBeforeTouchingDatabaseWithMessageContainingConnectionStringName()
    {
        using var factory = new ProductionGuardOverridesConnectionStringOnlyAfterBuildFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("ConnectionStrings:DefaultConnection", exception.Message, StringComparison.Ordinal);
        Assert.False(
            (object)exception is NpgsqlException,
            "InvalidOperationException aqui é a ConnectionStringGuard abortando antes do EF tocar o banco. " +
            "Se este teste algum dia capturar um NpgsqlException, a guarda foi retirada do Program.cs: " +
            "AddInfrastructure lê ConnectionStrings:DefaultConnection de builder.Configuration ANTES do " +
            "builder.Build(), então o override que este fixture injeta via ConfigureAppConfiguration nunca " +
            "chega ao NpgsqlDataSource — só chega à ConnectionStringGuard, que lê app.Configuration DEPOIS " +
            "do Build(). Sem a guarda, o boot segue com a string do appsettings.json " +
            "(Host=localhost;Port=5435;Database=custodia) e morre tentando conectar de verdade, com um " +
            "NpgsqlException cujo conteúdo varia conforme haja ou não um Postgres na porta 5435 da máquina.");
    }

    [Fact]
    public void Boot_Testing_WithGuardDisabledByEnvironment_ShouldNotFailToStart()
    {
        using var factory = new TestingWithoutConnectionCredentialFactory();

        _ = factory.Services;
    }

    private sealed class ProductionGuardOverridesConnectionStringOnlyAfterBuildFactory : WebApplicationFactory<Program>
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

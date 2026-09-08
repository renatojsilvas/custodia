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
            "O QUE ESTE TESTE PROVA: que em Production o boot aborta com InvalidOperationException citando a " +
            "chave de configuração, e não com um erro de conexão — ou seja, que a ConnectionStringGuard está " +
            "LIGADA no Program.cs e roda antes de o EF tocar o banco. Retirar a chamada da guarda deixa este " +
            "teste vermelho com NpgsqlException, e é esse o diagnóstico a procurar. " +
            "O QUE ESTE TESTE NÃO PROVA, e a distinção é o motivo de ele existir: nada sobre QUAL string o " +
            "EF usaria sem a guarda. O override deste fixture é redundante — o appsettings.json já não tem " +
            "credencial, então a guarda reprovaria mesmo sem ele, e o teste passa igual com o fixture vazio. " +
            "Medido à parte, e por isso não afirmado aqui: AddInfrastructure lê a connection string de " +
            "builder.Configuration ANTES do builder.Build(), então o override nunca alcança o " +
            "NpgsqlDataSource; sem a guarda o boot morre com a string do appsettings.json, e a mensagem " +
            "concreta varia conforme haja ou não um Postgres na porta 5435 da máquina.");
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

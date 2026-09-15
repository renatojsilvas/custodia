using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Custodia.API.Tests.Extensions;

public sealed class HubConfigGuardBootTests
{
    [Fact]
    public void Boot_Production_ComHubBaseUrlVazia_FalhaComMensagemContendoAChaveDeConfiguracao()
    {
        using var factory = new ProductionSemHubConfigFactory(
            baseUrl: "", apiKey: "hub-api-key-com-mais-de-trinta-e-dois-caracteres");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("Hub:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Production_ComHubApiKeyVazia_FalhaComMensagemContendoAChaveDeConfiguracao()
    {
        using var factory = new ProductionSemHubConfigFactory(
            baseUrl: "http://hub-precos-app:8080/", apiKey: "");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("Hub:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Production_ComHubApiKeyAbaixoDoComprimentoMinimo_FalhaComMensagemSobreOMinimo()
    {
        using var factory = new ProductionSemHubConfigFactory(
            baseUrl: "http://hub-precos-app:8080/", apiKey: new string('a', 31));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("Hub:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("abaixo do mínimo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Production_ComHubApiKeyPlaceholderBloqueada_FalhaComMensagemSobreOPlaceholder()
    {
        using var factory = new ProductionSemHubConfigFactory(
            baseUrl: "http://hub-precos-app:8080/", apiKey: "CHANGE-ME-IN-PRODUCTION-padded-xxxx");

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("Hub:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("placeholder conhecido", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Testing_SemHubBaseUrlNemHubApiKey_NaoFalhaAoSubir()
    {
        using var factory = new TestingWithoutHubConfigFactory();

        _ = factory.Services;
    }

    private sealed class ProductionSemHubConfigFactory(string baseUrl, string apiKey)
        : WebApplicationFactory<Program>
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
                    ["RabbitMq:Host"] = "localhost",
                    ["RabbitMq:User"] = "user",
                    ["RabbitMq:Password"] = "senha",
                    ["Hub:BaseUrl"] = baseUrl,
                    ["Hub:ApiKey"] = apiKey,
                });
            });
        }
    }

    private sealed class TestingWithoutHubConfigFactory : WebApplicationFactory<Program>
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

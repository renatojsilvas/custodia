using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Custodia.API.Tests.Extensions;

public sealed class BootstrapPrecosConfigGuardBootTests
{
    [Fact]
    public void Boot_Testing_ComTamanhoDeFatiaAcimaDe200_FalhaComMensagemContendoAChaveDeConfiguracao()
    {
        using var factory = new TestingComTamanhoDeFatiaInvalidoFactory(500);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.Services);

        Assert.Contains("Decisao:BootstrapPrecosTamanhoFatia", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_Testing_ComTamanhoDeFatiaPadrao_NaoFalhaAoSubir()
    {
        using var factory = new TestingComTamanhoDeFatiaInvalidoFactory(tamanhoFatia: null);

        _ = factory.Services;
    }

    private sealed class TestingComTamanhoDeFatiaInvalidoFactory(int? tamanhoFatia) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var dados = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake;Timeout=2",
                    ["ApiKey:Key"] = "",
                };

                if (tamanhoFatia is not null)
                {
                    dados["Decisao:BootstrapPrecosTamanhoFatia"] = tamanhoFatia.Value.ToString();
                }

                config.AddInMemoryCollection(dados);
            });
        }
    }
}

using Custodia.API.Extensions;
using Microsoft.Extensions.Configuration;

namespace Custodia.API.Tests.Extensions;

public sealed class BootstrapPrecosConfigGuardTests
{
    private static IConfiguration ConfiguracaoCom(int? tamanhoFatia)
    {
        var dados = new Dictionary<string, string?>();
        if (tamanhoFatia is not null)
        {
            dados["Decisao:BootstrapPrecosTamanhoFatia"] = tamanhoFatia.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
    }

    [Fact]
    public void Validate_SemConfiguracao_UsaODefaultENaoLanca() =>
        BootstrapPrecosConfigGuard.Validate(ConfiguracaoCom(null));

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(200)]
    public void Validate_ComTamanhoDentroDoIntervalo_NaoLanca(int tamanhoFatia) =>
        BootstrapPrecosConfigGuard.Validate(ConfiguracaoCom(tamanhoFatia));

    [Fact]
    public void Validate_ComTamanhoZero_Lanca()
    {
        var act = () => BootstrapPrecosConfigGuard.Validate(ConfiguracaoCom(0));

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Decisao:BootstrapPrecosTamanhoFatia", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ComTamanhoAcimaDe200_Lanca()
    {
        var act = () => BootstrapPrecosConfigGuard.Validate(ConfiguracaoCom(201));

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Decisao:BootstrapPrecosTamanhoFatia", exception.Message, StringComparison.Ordinal);
        Assert.Contains("201", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ComTamanhoNegativo_Lanca()
    {
        var act = () => BootstrapPrecosConfigGuard.Validate(ConfiguracaoCom(-1));

        Assert.Throws<InvalidOperationException>(act);
    }
}

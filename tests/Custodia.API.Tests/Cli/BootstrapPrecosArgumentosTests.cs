using Custodia.API.Cli;

namespace Custodia.API.Tests.Cli;

public sealed class BootstrapPrecosArgumentosTests
{
    [Fact]
    public void Interpretar_SemArgumentos_RetornaDesdeEAteNulos()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(["--bootstrap-precos"]);

        Assert.True(resultado.EhValido);
        Assert.Null(resultado.Desde);
        Assert.Null(resultado.Ate);
    }

    [Fact]
    public void Interpretar_ComDesdeEAteValidos_RetornaAsDatas()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(
            ["--bootstrap-precos", "--desde", "2026-01-01", "--ate", "2026-01-03"]);

        Assert.True(resultado.EhValido);
        Assert.Equal(new DateOnly(2026, 1, 1), resultado.Desde);
        Assert.Equal(new DateOnly(2026, 1, 3), resultado.Ate);
    }

    [Fact]
    public void Interpretar_ComDesdeIlegivel_RetornaErroDeUsoCitandoOArgumentoEOFormato()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(
            ["--bootstrap-precos", "--desde", "LIXO-INVALIDO", "--ate", "2026-01-03"]);

        Assert.False(resultado.EhValido);
        Assert.Null(resultado.Desde);
        Assert.Null(resultado.Ate);
        Assert.Contains("--desde", resultado.ErroDeUso, StringComparison.Ordinal);
        Assert.Contains("yyyy-MM-dd", resultado.ErroDeUso, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpretar_ComAteIlegivel_RetornaErroDeUsoCitandoOArgumentoEOFormato()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(
            ["--bootstrap-precos", "--desde", "2026-01-01", "--ate", "LIXO-INVALIDO"]);

        Assert.False(resultado.EhValido);
        Assert.Contains("--ate", resultado.ErroDeUso, StringComparison.Ordinal);
        Assert.Contains("yyyy-MM-dd", resultado.ErroDeUso, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpretar_ComDesdeSemValor_RetornaErroDeUso()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(["--bootstrap-precos", "--desde"]);

        Assert.False(resultado.EhValido);
        Assert.Contains("--desde", resultado.ErroDeUso, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpretar_ComAteSemValor_RetornaErroDeUso()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(
            ["--bootstrap-precos", "--desde", "2026-01-01", "--ate"]);

        Assert.False(resultado.EhValido);
        Assert.Contains("--ate", resultado.ErroDeUso, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpretar_ComArgumentoDesconhecido_RetornaErroDeUso()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(["--bootstrap-precos", "--rebolisco", "2026-01-01"]);

        Assert.False(resultado.EhValido);
        Assert.Contains("--rebolisco", resultado.ErroDeUso, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpretar_ComDesdeMaiorQueAte_NaoRejeitaNoParser_ARegraEDaCamadaDeAplicacao()
    {
        var resultado = BootstrapPrecosArgumentos.Interpretar(
            ["--bootstrap-precos", "--desde", "2026-01-10", "--ate", "2026-01-03"]);

        Assert.True(resultado.EhValido);
        Assert.Equal(new DateOnly(2026, 1, 10), resultado.Desde);
        Assert.Equal(new DateOnly(2026, 1, 3), resultado.Ate);
    }
}

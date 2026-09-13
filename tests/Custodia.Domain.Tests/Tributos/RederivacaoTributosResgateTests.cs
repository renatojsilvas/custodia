using Custodia.Domain.Movimentos;
using Custodia.Domain.Tests.Movimentos;
using Custodia.Domain.Tributos;

namespace Custodia.Domain.Tests.Tributos;

public sealed class RederivacaoTributosResgateTests
{
    private const string ClienteId = "cliente-1";
    private const string InstrumentoId = "td:tesouro-selic-2029";

    private static DateOnly Dia(int offset) => new DateOnly(2026, 1, 1).AddDays(offset);

    private static DateTimeOffset Instante(int offset) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(offset);

    [Fact]
    public void Rederivar_ExcluiOProprioResgateDaFila_NaoSeAutoConsome()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "compra-1");

        var resgate = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(11), Instante(11), -10m, 1200m, "resgate-1");

        var resultado = RederivacaoTributosResgate.Rederivar([compra, resgate], resgate);

        Assert.True(resultado.Consumo.CoberturaCompleta);
        var loteConsumido = Assert.Single(resultado.Consumo.Lotes);
        Assert.Equal(10m, loteConsumido.Quantidade);
        Assert.Equal(100m, loteConsumido.CustoUnitario);
        Assert.Equal(10, loteConsumido.Prazo);
        Assert.Equal(15.30m, resultado.Tributos.Ir);
        Assert.Equal(132.00m, resultado.Tributos.Iof);
    }

    [Fact]
    public void Rederivar_ComFilaVazia_CustoZeroEPrazoZero_IrDeVinteEDoisEMeioPorCentoSemIof()
    {
        var resgate = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(5), Instante(5), -10m, 1000m, "resgate-descoberto");

        var resultado = RederivacaoTributosResgate.Rederivar([resgate], resgate);

        Assert.False(resultado.Consumo.CoberturaCompleta);
        Assert.Equal(10m, resultado.Consumo.QuantidadeDescoberta);
        Assert.Equal(0m, resultado.Tributos.Iof);
        Assert.Equal(225.00m, resultado.Tributos.Ir);
    }

    [Fact]
    public void Rederivar_ComPrejuizoEmTodosOsLotes_IrEIofZerados()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 3000m, "compra-prejuizo");

        var resgate = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(6), Instante(6), -10m, 1000m, "resgate-prejuizo");

        var resultado = RederivacaoTributosResgate.Rederivar([compra, resgate], resgate);

        Assert.Equal(0m, resultado.Tributos.Ir);
        Assert.Equal(0m, resultado.Tributos.Iof);
    }
}

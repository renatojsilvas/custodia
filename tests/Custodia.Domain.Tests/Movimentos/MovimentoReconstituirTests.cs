using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Tests.Movimentos;

public sealed class MovimentoReconstituirTests
{
    private static readonly DateOnly DataEvento = new(2026, 9, 11);
    private static readonly DateTimeOffset RegistradoEm = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reconstituir_ComTodosOsCampos_MaterializaOAgregadoComIdEDemaisCamposPreenchidos()
    {
        var movimento = Movimento.Reconstituir(
            42,
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Ajuste,
            DataEvento,
            RegistradoEm,
            -10m,
            -1000m,
            "trade-1",
            refEstorno: 7);

        Assert.Equal(42, movimento.Id);
        Assert.Equal("cliente-1", movimento.ClienteId);
        Assert.Equal("td:tesouro-selic-2029", movimento.InstrumentoId);
        Assert.Equal(TipoMovimento.Ajuste, movimento.Tipo);
        Assert.Equal(DataEvento, movimento.DataEvento);
        Assert.Equal(RegistradoEm, movimento.RegistradoEm);
        Assert.Equal(-10m, movimento.QtdDelta);
        Assert.Equal(-1000m, movimento.ValorFinanceiro);
        Assert.Equal("trade-1", movimento.RefExterna);
        Assert.Equal(7, movimento.RefEstorno);
    }

    [Fact]
    public void Reconstituir_ComTipoNulo_DeveLancarArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Movimento.Reconstituir(
            1,
            "cliente-1",
            "td:tesouro-selic-2029",
            null!,
            DataEvento,
            RegistradoEm,
            10m,
            1000m,
            "trade-1",
            refEstorno: null));
    }

    [Fact]
    public void Reconstituir_ComQtdDeltaQueCreateRejeitariaPorMagnitude_NaoRevalidaEDevolveOAgregadoComOValorGravado()
    {
        var qtdDeltaQueCreateRejeitaria = SchemaNumericLimits.QuantidadeLimiteSuperiorExclusivo;

        var criacaoEquivalente = Movimento.Create(
            "cliente-1", "td:tesouro-selic-2029", TipoMovimento.Compra, DataEvento, RegistradoEm,
            qtdDeltaQueCreateRejeitaria, 1000m, "trade-1");
        Assert.True(criacaoEquivalente.IsFailure);

        var movimento = Movimento.Reconstituir(
            1,
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            qtdDeltaQueCreateRejeitaria,
            1000m,
            "trade-1",
            refEstorno: null);

        Assert.Equal(qtdDeltaQueCreateRejeitaria, movimento.QtdDelta);
    }

    [Fact]
    public void Reconstituir_ComValorFinanceiroQueCreateRejeitariaPorEscala_NaoRevalidaEDevolveOAgregadoComOValorGravado()
    {
        var valorFinanceiroQueCreateRejeitaria = 100.005m;

        var criacaoEquivalente = Movimento.Create(
            "cliente-1", "td:tesouro-selic-2029", TipoMovimento.Compra, DataEvento, RegistradoEm,
            10m, valorFinanceiroQueCreateRejeitaria, "trade-1");
        Assert.True(criacaoEquivalente.IsFailure);

        var movimento = Movimento.Reconstituir(
            1,
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            10m,
            valorFinanceiroQueCreateRejeitaria,
            "trade-1",
            refEstorno: null);

        Assert.Equal(valorFinanceiroQueCreateRejeitaria, movimento.ValorFinanceiro);
    }
}

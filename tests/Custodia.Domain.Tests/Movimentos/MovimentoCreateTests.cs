using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Tests.Movimentos;

public sealed class MovimentoCreateTests
{
    private static readonly DateOnly DataEvento = new(2026, 9, 11);
    private static readonly DateTimeOffset RegistradoEm = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ComDadosValidos_DeveTerSucessoEPreencherTodosOsCampos()
    {
        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            10m,
            1000m,
            "trade-1");

        Assert.True(resultado.IsSuccess);
        var movimento = resultado.Value;
        Assert.Equal("cliente-1", movimento.ClienteId);
        Assert.Equal("td:tesouro-selic-2029", movimento.InstrumentoId);
        Assert.Equal(TipoMovimento.Compra, movimento.Tipo);
        Assert.Equal(DataEvento, movimento.DataEvento);
        Assert.Equal(RegistradoEm, movimento.RegistradoEm);
        Assert.Equal(10m, movimento.QtdDelta);
        Assert.Equal(1000m, movimento.ValorFinanceiro);
        Assert.Equal("trade-1", movimento.RefExterna);
        Assert.Null(movimento.RefEstorno);
    }

    [Fact]
    public void Create_ComTipoNulo_DeveLancarArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            null!,
            DataEvento,
            RegistradoEm,
            10m,
            1000m,
            "trade-1"));
    }

    [Fact]
    public void Create_ComQtdDeltaNoLimiteSuperiorDeMagnitude_DeveFalhar()
    {
        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            SchemaNumericLimits.QuantidadeLimiteSuperiorExclusivo,
            1000m,
            "trade-1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoErrors.QtdDeltaExcedePrecisaoSuportada, resultado.Error);
    }

    [Fact]
    public void Create_ComQtdDeltaNegativoExcedendoMagnitude_DeveFalhar()
    {
        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Venda,
            DataEvento,
            RegistradoEm,
            -SchemaNumericLimits.QuantidadeLimiteSuperiorExclusivo,
            1000m,
            "trade-1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoErrors.QtdDeltaExcedePrecisaoSuportada, resultado.Error);
    }

    [Fact]
    public void Create_ComQtdDeltaExcedendoEscalaSuportada_DeveFalhar()
    {
        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            1.123456789m,
            1000m,
            "trade-1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoErrors.QtdDeltaExcedePrecisaoSuportada, resultado.Error);
    }

    [Fact]
    public void Create_ComValorFinanceiroNoLimiteSuperiorDeMagnitude_DeveFalhar()
    {
        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            10m,
            SchemaNumericLimits.ValorLimiteSuperiorExclusivo,
            "trade-1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoErrors.ValorFinanceiroExcedePrecisaoSuportada, resultado.Error);
    }

    [Fact]
    public void Create_ComValorFinanceiroExcedendoEscalaSuportada_DeveFalhar()
    {
        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            10m,
            1.123m,
            "trade-1");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoErrors.ValorFinanceiroExcedePrecisaoSuportada, resultado.Error);
    }

    [Fact]
    public void Create_ComQtdDeltaEValorFinanceiroDentroDoLimite_DeveTerSucesso()
    {
        var qtdDeltaValido = SchemaNumericLimits.QuantidadeLimiteSuperiorExclusivo - 1m;
        var valorFinanceiroValido = SchemaNumericLimits.ValorLimiteSuperiorExclusivo - 1m;

        var resultado = Movimento.Create(
            "cliente-1",
            "td:tesouro-selic-2029",
            TipoMovimento.Compra,
            DataEvento,
            RegistradoEm,
            qtdDeltaValido,
            valorFinanceiroValido,
            "trade-1");

        Assert.True(resultado.IsSuccess);
    }
}

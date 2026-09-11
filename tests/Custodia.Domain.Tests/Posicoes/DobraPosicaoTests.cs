using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Tests.Movimentos;

namespace Custodia.Domain.Tests.Posicoes;

public sealed class DobraPosicaoTests
{
    private const string ClienteId = "cliente-1";
    private const string InstrumentoId = "td:tesouro-selic-2029";

    private static DateOnly Dia(int offset) => new DateOnly(2026, 1, 1).AddDays(offset);

    private static DateTimeOffset Instante(int offset) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(offset);

    [Fact]
    public void Dobrar_SemMovimentos_RetornaEstadoZero()
    {
        var resultado = DobraPosicao.Dobrar([]);

        Assert.Equal(PosicaoTresColunas.Zero, resultado);
    }

    [Fact]
    public void AplicarIncremental_ComPrimeiraLinhaDaChaveEMaxNulo_AplicaDeltaIncrementalENaoExigeRedobra()
    {
        var primeiraCompra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");

        var resultado = DobraPosicao.AplicarIncremental(PosicaoTresColunas.Zero, primeiraCompra, maxDataEventoDaChave: null);

        Assert.False(resultado.ExigeRedobraDaChave);
        Assert.NotNull(resultado.Estado);
        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), resultado.Estado);
    }

    [Fact]
    public void AplicarIncremental_ComDataEventoMaiorOuIgualAoMaximoDaChave_AplicaComoDelta()
    {
        var estadoAtual = new PosicaoTresColunas(10m, 1000m, 100m);
        var proximaCompra = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 5m, 500m, "trade-2");

        var resultado = DobraPosicao.AplicarIncremental(estadoAtual, proximaCompra, maxDataEventoDaChave: Dia(1));

        Assert.False(resultado.ExigeRedobraDaChave);
        Assert.Equal(new PosicaoTresColunas(15m, 1500m, 100m), resultado.Estado);
    }

    [Fact]
    public void AplicarIncremental_ComDataEventoAnteriorAoMaximoDaChave_ExigeRedobra()
    {
        var estadoAtual = new PosicaoTresColunas(6m, 600m, 100m);
        var compraRetroativa = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 2000m, "trade-retroativo");

        var resultado = DobraPosicao.AplicarIncremental(estadoAtual, compraRetroativa, maxDataEventoDaChave: Dia(2));

        Assert.True(resultado.ExigeRedobraDaChave);
        Assert.Null(resultado.Estado);
    }

    [Fact]
    public void AplicarIncremental_ComMovimentoAjuste_SempreExigeRedobraMesmoSendoOUltimoDaOrdem()
    {
        var estadoAtual = new PosicaoTresColunas(10m, 1000m, 100m);
        var ajuste = MovimentoTestExtensions.MovimentoValido(
            4, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(5), Instante(5), 4m, -600m, "estorno-trade-2", 2);

        var resultado = DobraPosicao.AplicarIncremental(estadoAtual, ajuste, maxDataEventoDaChave: Dia(1));

        Assert.True(resultado.ExigeRedobraDaChave);
        Assert.Null(resultado.Estado);
    }

    [Fact]
    public void Dobrar_ComCompraCompraVendaCompra_AplicaTabelaPorTipoNaOrdemDeChegada()
    {
        var m1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var m2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 5m, 500m, "trade-2");
        var m3 = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(3), Instante(3), -8m, 800m, "trade-3");
        var m4 = MovimentoTestExtensions.MovimentoValido(
            4, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(4), Instante(4), 3m, 330m, "trade-4");

        var resultado = DobraPosicao.Dobrar([m1, m2, m3, m4]);

        Assert.Equal(new PosicaoTresColunas(10m, 1030m, 103m), resultado);
    }

    [Fact]
    public void Dobrar_ComAporte_DobraExatamenteComoCompra()
    {
        var aporte = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Aporte, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");

        var resultadoAporte = DobraPosicao.Dobrar([aporte]);
        var resultadoCompra = DobraPosicao.Dobrar([compra]);

        Assert.Equal(resultadoCompra, resultadoAporte);
        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), resultadoAporte);
    }

    [Fact]
    public void Dobrar_ComEstornoDaVenda_RestauraEstadoAnteriorAVendaEDistingueDaRegraIngenua()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var venda = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(2), Instante(2), -4m, 600m, "trade-2");
        var estorno = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(2), Instante(3), 4m, -600m, "estorno-trade-2", 2);

        var resultado = DobraPosicao.Dobrar([compra, venda, estorno]);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), resultado);
        Assert.NotEqual(1200m, resultado.CustoTotal);
        Assert.NotEqual(120m, resultado.PrecoMedio);
    }

    [Fact]
    public void Dobrar_ComVendaSemCompraSeguidaDeCompraQueZera_RetornaEstadoCanonicoSemDivisaoPorZero()
    {
        var vendaSemCompra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(1), Instante(1), -10m, 1000m, "trade-1");
        var compraQueZera = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 10m, 1000m, "trade-2");

        var resultado = DobraPosicao.Dobrar([vendaSemCompra, compraQueZera]);

        Assert.Equal(PosicaoTresColunas.Zero, resultado);
        Assert.True(resultado.PrecoMedio >= 0m);
    }

    [Fact]
    public void Dobrar_ComVendaSemCompraSeguidaDeCompraQueNaoZera_MantemPrecoMedioInalterado()
    {
        var vendaSemCompra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(1), Instante(1), -10m, 1000m, "trade-1");
        var compraQueNaoZera = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 4m, 400m, "trade-2");

        var resultado = DobraPosicao.Dobrar([vendaSemCompra, compraQueNaoZera]);

        Assert.Equal(-6m, resultado.Quantidade);
        Assert.Equal(400m, resultado.CustoTotal);
        Assert.Equal(0m, resultado.PrecoMedio);
        Assert.NotEqual(-66.67m, decimal.Round(resultado.PrecoMedio, 2));
        Assert.True(resultado.PrecoMedio >= 0m);
    }

    [Fact]
    public void Dobrar_ComVendaSemCompraSeguidaDeCompraQueCruza_UsaPrecoUnitarioDaLinhaQueCruza()
    {
        var vendaSemCompra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(1), Instante(1), -10m, 1000m, "trade-1");
        var compraQueCruza = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 20m, 2000m, "trade-2");

        var resultado = DobraPosicao.Dobrar([vendaSemCompra, compraQueCruza]);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), resultado);
        Assert.NotEqual(200m, resultado.PrecoMedio);
        Assert.NotEqual(2000m, resultado.CustoTotal);
        Assert.True(resultado.PrecoMedio >= 0m);
    }

    [Fact]
    public void Dobrar_ComMovimentoRetroativo_ReDobraAChaveInteiraEnaoAplicaComoDelta()
    {
        var compraD1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var vendaD2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(2), Instante(2), -4m, 500m, "trade-2");
        var compraRetroativaD0 = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 2000m, "trade-3");

        var estadoAntesDaRetroativa = DobraPosicao.Dobrar([compraD1, vendaD2]);
        var resultadoIncremental = DobraPosicao.AplicarIncremental(estadoAntesDaRetroativa, compraRetroativaD0, maxDataEventoDaChave: Dia(2));

        Assert.True(resultadoIncremental.ExigeRedobraDaChave);

        var resultadoRedobrado = DobraPosicao.Dobrar([compraD1, vendaD2, compraRetroativaD0]);

        Assert.Equal(new PosicaoTresColunas(16m, 2400m, 150m), resultadoRedobrado);
        Assert.NotEqual(2600m, resultadoRedobrado.CustoTotal);
        Assert.NotEqual(162.50m, resultadoRedobrado.PrecoMedio);
    }

    [Fact]
    public void Dobrar_ComCompraEmCaixaBrlComQtdDeltaNegativo_DobraPelaRegraDeCaixaNaoPorAcumuloDeValorFinanceiro()
    {
        var compraDebitandoCaixa = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentosCaixa.Brl, TipoMovimento.Compra, Dia(1), Instante(1), -500m, 500m, "trade-1");

        var resultado = DobraPosicao.Dobrar([compraDebitandoCaixa]);

        Assert.Equal(-500m, resultado.Quantidade);
        Assert.Equal(-500m, resultado.CustoTotal);
        Assert.NotEqual(500m, resultado.CustoTotal);
        Assert.Equal(1.000000m, resultado.PrecoMedio);
    }

    [Fact]
    public void Dobrar_ComPernaDeALiquidarPositivaEPernaDeBrlNegativaNaReaplicacaoDoMesmoDia_SomaPrecoMedioVezesQuantidadeDaZeroENaoNovecentos()
    {
        var creditoALiquidarDoResgate = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, Dia(1), Instante(1), 900m, 900m, "resgate-1");
        var debitoBrlDaReaplicacao = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao, Dia(1), Instante(1), -900m, 900m, "reaplicacao-1");

        var resultadoALiquidar = DobraPosicao.Dobrar([creditoALiquidarDoResgate]);
        var resultadoBrl = DobraPosicao.Dobrar([debitoBrlDaReaplicacao]);

        Assert.Equal(1.000000m, resultadoALiquidar.PrecoMedio);
        Assert.Equal(1.000000m, resultadoBrl.PrecoMedio);

        var somaPrecoMedioVezesQuantidade =
            resultadoALiquidar.PrecoMedio * resultadoALiquidar.Quantidade +
            resultadoBrl.PrecoMedio * resultadoBrl.Quantidade;

        Assert.Equal(0m, somaPrecoMedioVezesQuantidade);
        Assert.NotEqual(900m, somaPrecoMedioVezesQuantidade);
    }

    [Fact]
    public void Dobrar_ComQuantidadeNegativaDeCaixaAlcancadaPorChaveQueNasceNegativaOuPorChaveQueVinhaPositiva_ProduzOMesmoPrecoMedioIgualAUm()
    {
        var chaveQueNasceNegativa = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao, Dia(1), Instante(1), -900m, 900m, "trade-1");

        var primeiraLinhaPositivaDaChave = MovimentoTestExtensions.MovimentoValido(
            1, "cliente-2", InstrumentosCaixa.Brl, TipoMovimento.Aporte, Dia(1), Instante(1), 1000m, 1000m, "trade-2");
        var segundaLinhaQueDerrubaParaNegativo = MovimentoTestExtensions.MovimentoValido(
            2, "cliente-2", InstrumentosCaixa.Brl, TipoMovimento.Liquidacao, Dia(2), Instante(2), -1900m, 1900m, "trade-3");

        var resultadoChaveNasceuNegativa = DobraPosicao.Dobrar([chaveQueNasceNegativa]);
        var resultadoChaveVinhaPositiva = DobraPosicao.Dobrar([primeiraLinhaPositivaDaChave, segundaLinhaQueDerrubaParaNegativo]);

        Assert.Equal(-900m, resultadoChaveNasceuNegativa.Quantidade);
        Assert.Equal(-900m, resultadoChaveVinhaPositiva.Quantidade);
        Assert.Equal(resultadoChaveNasceuNegativa.PrecoMedio, resultadoChaveVinhaPositiva.PrecoMedio);
        Assert.Equal(1.000000m, resultadoChaveNasceuNegativa.PrecoMedio);
    }

    [Fact]
    public void EstadoErradoDePrecoMedioZeroEmCaixaNegativo_PassaNaGuardaDeSinalMasNaoSatisfazCustoTotalIgualPrecoMedioVezesQuantidade()
    {
        var estadoErradoRejeitado = new PosicaoTresColunas(-900m, -900m, 0m);

        Assert.True(estadoErradoRejeitado.PrecoMedio >= 0m);
        Assert.NotEqual(estadoErradoRejeitado.CustoTotal, estadoErradoRejeitado.PrecoMedio * estadoErradoRejeitado.Quantidade);
    }

    [Fact]
    public void Dobrar_ComLinhaDeCaixaComQuantidadePositiva_FixaPrecoMedioEmUmECustoIgualAQuantidade()
    {
        var aporteEmCaixa = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentosCaixa.Brl, TipoMovimento.Aporte, Dia(1), Instante(1), 500m, 500m, "trade-1");

        var resultado = DobraPosicao.Dobrar([aporteEmCaixa]);

        Assert.Equal(new PosicaoTresColunas(500m, 500m, 1.000000m), resultado);
    }

    [Fact]
    public void Dobrar_ComCupom_NaoAlteraCustoTotalNemPrecoMedio()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var cupom = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Cupom, Dia(2), Instante(2), 0m, 50m, "trade-2");

        var resultado = DobraPosicao.Dobrar([compra, cupom]);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), resultado);
    }

    [Fact]
    public void Dobrar_ComEstornoDeEstorno_LinhaOriginalVoltaASerEfetiva()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var venda = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(2), Instante(2), -4m, 600m, "trade-2");
        var estorno = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(2), Instante(3), 4m, -600m, "estorno-trade-2", 2);
        var estornoDoEstorno = MovimentoTestExtensions.MovimentoValido(
            4, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(2), Instante(4), -4m, 600m, "estorno-estorno-trade-2", 3);

        var resultado = DobraPosicao.Dobrar([compra, venda, estorno, estornoDoEstorno]);
        var resultadoSemNenhumEstorno = DobraPosicao.Dobrar([compra, venda]);

        Assert.Equal(resultadoSemNenhumEstorno, resultado);
        Assert.Equal(new PosicaoTresColunas(6m, 600m, 100m), resultado);
    }

    [Fact]
    public void Dobrar_ComCorteEmDataAnterior_IgnoraMovimentosPosterioresAoCorte()
    {
        var m1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var m2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 5m, 500m, "trade-2");

        var resultadoComCorte = DobraPosicao.Dobrar([m1, m2], corte: Dia(1));
        var resultadoSemCorte = DobraPosicao.Dobrar([m1, m2]);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), resultadoComCorte);
        Assert.Equal(new PosicaoTresColunas(15m, 1500m, 100m), resultadoSemCorte);
    }

    [Fact]
    public void Dobrar_ComMesmaCestaEmOrdensDeInsercaoDiferentes_ProduzMesmoResultado()
    {
        var m1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var m2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(1), Instante(1), -5m, 500m, "trade-2");
        var m3 = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 2000m, "trade-3");

        var resultadoOrdemEscrita = DobraPosicao.Dobrar([m1, m2, m3]);
        var resultadoOrdemEmbaralhada = DobraPosicao.Dobrar([m3, m1, m2]);
        var resultadoOutraOrdemEmbaralhada = DobraPosicao.Dobrar([m2, m3, m1]);

        Assert.Equal(resultadoOrdemEscrita, resultadoOrdemEmbaralhada);
        Assert.Equal(resultadoOrdemEscrita, resultadoOutraOrdemEmbaralhada);

        var quantidadeEsperada = 15m;
        var custoEsperado = 2500m;
        var precoMedioEsperado = custoEsperado / quantidadeEsperada;
        Assert.Equal(new PosicaoTresColunas(quantidadeEsperada, custoEsperado, precoMedioEsperado), resultadoOrdemEscrita);
    }

    [Fact]
    public void Dobrar_ComDataEventoERegistradoEmEmpatados_DesempataPorId()
    {
        var compraBaixa = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var venda = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(1), Instante(1), -5m, 500m, "trade-2");
        var compraAlta = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 2000m, "trade-3");

        var compraBaixaComIdInvertido = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var vendaComIdRepetido = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(1), Instante(1), -5m, 500m, "trade-2");
        var compraAltaComIdInvertido = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 2000m, "trade-3");

        var resultadoOrdemOriginal = DobraPosicao.Dobrar([compraBaixa, venda, compraAlta]);
        var resultadoComIdsInvertidos = DobraPosicao.Dobrar([compraBaixaComIdInvertido, vendaComIdRepetido, compraAltaComIdInvertido]);

        Assert.NotEqual(resultadoOrdemOriginal.PrecoMedio, resultadoComIdsInvertidos.PrecoMedio);
    }
}

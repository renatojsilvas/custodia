using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Tests.Movimentos;

namespace Custodia.Domain.Tests.Posicoes;

public sealed class FilaDeLotesTests
{
    private const string ClienteId = "cliente-1";
    private const string InstrumentoId = "td:tesouro-selic-2029";

    private static DateOnly Dia(int offset) => new DateOnly(2026, 1, 1).AddDays(offset);

    private static DateTimeOffset Instante(int offset) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(offset);

    private static CortePosicional CorteDoResgate(int diaOffset) => new(Dia(diaOffset), Instante(diaOffset));

    [Fact]
    public void Reconstruir_SemMovimentos_RetornaFilaVazia()
    {
        var fila = FilaDeLotes.Reconstruir([], CortePosicional.Infinito);

        Assert.Empty(fila);
    }

    [Fact]
    public void Reconstruir_ComUmaCompra_ProduzUmLoteComCustoUnitarioEDataDaCompra()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");

        var fila = FilaDeLotes.Reconstruir([compra], CortePosicional.Infinito);

        var lote = Assert.Single(fila);
        Assert.Equal(10m, lote.Quantidade);
        Assert.Equal(100m, lote.CustoUnitario);
        Assert.Equal(Dia(1), lote.DataAquisicao);
    }

    [Fact]
    public void Reconstruir_ComAporte_FormaLoteAssimComoCompra()
    {
        var aporte = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Aporte, Dia(1), Instante(1), 10m, 1000m, "trade-1");

        var fila = FilaDeLotes.Reconstruir([aporte], CortePosicional.Infinito);

        var lote = Assert.Single(fila);
        Assert.Equal(10m, lote.Quantidade);
        Assert.Equal(100m, lote.CustoUnitario);
    }

    [Fact]
    public void Reconstruir_ComVendaParcial_MantemSaldoDoLoteComDataOriginal()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var venda = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(2), Instante(2), -4m, 600m, "trade-2");

        var fila = FilaDeLotes.Reconstruir([compra, venda], CortePosicional.Infinito);

        var lote = Assert.Single(fila);
        Assert.Equal(6m, lote.Quantidade);
        Assert.Equal(100m, lote.CustoUnitario);
        Assert.Equal(Dia(1), lote.DataAquisicao);
    }

    [Fact]
    public void Reconstruir_ComDuasComprasEVendaQueAtravessaAmbas_ConsomePorFifoNaOrdemDeChegada()
    {
        var compra1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var compra2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 5m, 600m, "trade-2");
        var venda = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(3), Instante(3), -12m, 1500m, "trade-3");

        var fila = FilaDeLotes.Reconstruir([compra1, compra2, venda], CortePosicional.Infinito);

        var loteRestante = Assert.Single(fila);
        Assert.Equal(3m, loteRestante.Quantidade);
        Assert.Equal(120m, loteRestante.CustoUnitario);
        Assert.Equal(Dia(2), loteRestante.DataAquisicao);
    }

    [Fact]
    public void Reconstruir_ComEstornoDeCompra_LoteNuncaExistiuNaFila()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var estornoDaCompra = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(1), Instante(2), -10m, -1000m, "estorno-trade-1", 1);

        var fila = FilaDeLotes.Reconstruir([compra, estornoDaCompra], CortePosicional.Infinito);

        Assert.Empty(fila);
    }

    [Fact]
    public void Reconstruir_ComEstornoDeVenda_LoteVoltaIntegralComADataOriginalDaCompra()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var venda = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(5), Instante(2), -4m, 600m, "trade-2");
        var estornoDaVenda = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(5), Instante(3), 4m, -600m, "estorno-trade-2", 2);

        var fila = FilaDeLotes.Reconstruir([compra, venda, estornoDaVenda], CortePosicional.Infinito);

        var lote = Assert.Single(fila);
        Assert.Equal(10m, lote.Quantidade);
        Assert.Equal(100m, lote.CustoUnitario);
        Assert.Equal(Dia(1), lote.DataAquisicao);
    }

    [Fact]
    public void Reconstruir_ComMovimentoDeCaixa_NuncaFormaLote()
    {
        var compraFinanciadaPorSaldo = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentosCaixa.Brl, TipoMovimento.Compra, Dia(1), Instante(1), 500m, 500m, "trade-1:brl-invertido-para-teste");

        var fila = FilaDeLotes.Reconstruir([compraFinanciadaPorSaldo], CortePosicional.Infinito);

        Assert.Empty(fila);
    }

    [Fact]
    public void Reconstruir_ComCorteEntreACompraEOSeuEstorno_LoteRevertidoNaoReapareceNaFila()
    {
        var compra = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(5), Instante(1), 10m, 1000m, "trade-1");
        var estornoDaCompra = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Ajuste, Dia(5), Instante(10), -10m, -1000m, "estorno-trade-1", 1);

        var corteEntreOAlvoEOReversor = new CortePosicional(Dia(5), Instante(5));

        var fila = FilaDeLotes.Reconstruir([compra, estornoDaCompra], corteEntreOAlvoEOReversor);

        Assert.Empty(fila);
    }

    [Fact]
    public void ConsumirParaResgate_ComFilaSuficienteEmUmUnicoLote_ConsomeApenasOSolicitado()
    {
        var fila = new List<Lote> { new(10m, 100m, Dia(1)) };

        var consumo = FilaDeLotes.ConsumirParaResgate(fila, 4m, CorteDoResgate(5));

        Assert.True(consumo.CoberturaCompleta);
        Assert.Equal(0m, consumo.QuantidadeDescoberta);
        var loteConsumido = Assert.Single(consumo.Lotes);
        Assert.Equal(4m, loteConsumido.Quantidade);
        Assert.Equal(100m, loteConsumido.CustoUnitario);
        Assert.Equal(Dia(1), loteConsumido.DataAquisicao);
        Assert.Equal(4, loteConsumido.Prazo);
    }

    [Fact]
    public void ConsumirParaResgate_ComFilaAtravessandoDoisLotes_ConsomeOsDoisPorFifo()
    {
        var fila = new List<Lote> { new(5m, 100m, Dia(1)), new(10m, 120m, Dia(2)) };

        var consumo = FilaDeLotes.ConsumirParaResgate(fila, 8m, CorteDoResgate(10));

        Assert.True(consumo.CoberturaCompleta);
        Assert.Equal(2, consumo.Lotes.Count);
        Assert.Equal(5m, consumo.Lotes[0].Quantidade);
        Assert.Equal(Dia(1), consumo.Lotes[0].DataAquisicao);
        Assert.Equal(9, consumo.Lotes[0].Prazo);
        Assert.Equal(3m, consumo.Lotes[1].Quantidade);
        Assert.Equal(Dia(2), consumo.Lotes[1].DataAquisicao);
        Assert.Equal(8, consumo.Lotes[1].Prazo);
    }

    [Fact]
    public void ConsumirParaResgate_ComFilaInsuficiente_HerdaCustoEDataDoUltimoLoteParaAQuantidadeDescoberta()
    {
        var fila = new List<Lote> { new(10m, 100m, Dia(1)) };

        var consumo = FilaDeLotes.ConsumirParaResgate(fila, 25m, CorteDoResgate(20));

        Assert.False(consumo.CoberturaCompleta);
        Assert.Equal(15m, consumo.QuantidadeDescoberta);
        Assert.Equal(2, consumo.Lotes.Count);

        var loteReal = consumo.Lotes[0];
        Assert.Equal(10m, loteReal.Quantidade);
        Assert.Equal(100m, loteReal.CustoUnitario);
        Assert.Equal(Dia(1), loteReal.DataAquisicao);
        Assert.Equal(19, loteReal.Prazo);

        var loteFantasma = consumo.Lotes[1];
        Assert.Equal(15m, loteFantasma.Quantidade);
        Assert.Equal(100m, loteFantasma.CustoUnitario);
        Assert.Equal(Dia(1), loteFantasma.DataAquisicao);
        Assert.Equal(19, loteFantasma.Prazo);
    }

    [Fact]
    public void ConsumirParaResgate_ComFilaTotalmenteVazia_CustoZeroEDataIgualADoResgate()
    {
        var consumo = FilaDeLotes.ConsumirParaResgate([], 4m, CorteDoResgate(20));

        Assert.False(consumo.CoberturaCompleta);
        Assert.Equal(4m, consumo.QuantidadeDescoberta);
        var loteFantasma = Assert.Single(consumo.Lotes);
        Assert.Equal(4m, loteFantasma.Quantidade);
        Assert.Equal(0m, loteFantasma.CustoUnitario);
        Assert.Equal(Dia(20), loteFantasma.DataAquisicao);
        Assert.Equal(0, loteFantasma.Prazo);
    }

    [Fact]
    public void ConsumirParaResgate_ComLoteAdquiridoAposADataDoResgate_NaoPodeSerConstruido()
    {
        var filaComLoteFuturo = new List<Lote> { new(10m, 100m, Dia(20)) };

        Assert.Throws<ArgumentOutOfRangeException>(() => FilaDeLotes.ConsumirParaResgate(filaComLoteFuturo, 4m, CorteDoResgate(5)));
    }

    [Fact]
    public void Reconstruir_SomaDaQuantidadeDosLotesVivos_IgualaAQuantidadeDaDobra()
    {
        var compra1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var compra2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 5m, 750m, "trade-2");
        var venda = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(3), Instante(3), -4m, 600m, "trade-3");

        var movimentos = new List<Movimento> { compra1, compra2, venda };

        var fila = FilaDeLotes.Reconstruir(movimentos, CortePosicional.Infinito);
        var dobra = DobraPosicao.Dobrar(movimentos);

        Assert.Equal(dobra.Quantidade, fila.Sum(lote => lote.Quantidade));
        Assert.NotEqual(dobra.CustoTotal, fila.Sum(lote => lote.Quantidade * lote.CustoUnitario));
    }

    [Fact]
    public void Reconstruir_ComOMesmoObjetoDeCorteQueADobra_SomaDaQuantidadeDosLotesVivosIgualaAQuantidadeDaDobra()
    {
        var compra1 = MovimentoTestExtensions.MovimentoValido(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "trade-1");
        var compra2 = MovimentoTestExtensions.MovimentoValido(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(2), Instante(2), 5m, 750m, "trade-2");
        var vendaDentroDoCorte = MovimentoTestExtensions.MovimentoValido(
            3, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(3), Instante(3), -4m, 600m, "trade-3");
        var vendaForaDoCorte = MovimentoTestExtensions.MovimentoValido(
            4, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(10), Instante(4), -3m, 500m, "trade-4");

        var movimentos = new List<Movimento> { compra1, compra2, vendaDentroDoCorte, vendaForaDoCorte };
        var corte = new CortePosicional(Dia(3), Instante(3));

        var fila = FilaDeLotes.Reconstruir(movimentos, corte);
        var dobra = DobraPosicao.Dobrar(movimentos, corte);

        Assert.Equal(dobra.Quantidade, fila.Sum(lote => lote.Quantidade));
        Assert.NotEqual(dobra.CustoTotal, fila.Sum(lote => lote.Quantidade * lote.CustoUnitario));
    }
}

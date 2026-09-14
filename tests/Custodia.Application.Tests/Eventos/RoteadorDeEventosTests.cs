using Custodia.Application.Eventos;
using Custodia.Domain.Eventos;

namespace Custodia.Application.Tests.Eventos;

public sealed class RoteadorDeEventosTests
{
    private const string PayloadTradeRegisteredValido = """
        {
          "v": 1, "tipo": "TradeRegistered",
          "tradeId": "op-7f3a", "clienteId": "cli-001",
          "instrumentoId": "td:tesouro-ipca-2035-05-15",
          "operacao": "aplicacao",
          "quantidade": "2.86000000", "valorFinanceiro": "10000.00",
          "dataEvento": "2026-08-01", "registradoEm": "2026-08-15T14:02:11Z"
        }
        """;

    private readonly RoteadorDeEventos _roteador = new();

    [Fact]
    public void Rotear_TradesRegisteredComPayloadValido_DevolveEscriturarComOEventoParseado()
    {
        var desfecho = _roteador.Rotear("trades.registered", PayloadTradeRegisteredValido);

        Assert.Equal(DesfechoRoteamentoTipo.Escriturar, desfecho.Tipo);
        Assert.NotNull(desfecho.Evento);
        Assert.Equal("op-7f3a", desfecho.Evento!.TradeId);
    }

    [Fact]
    public void Rotear_TradesRegisteredComPayloadInvalido_DevolveEstacionarComPayloadInvalido()
    {
        var desfecho = _roteador.Rotear("trades.registered", "{\"v\": 1}");

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.PayloadInvalido, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_TradesRegisteredComVersaoNaoSuportada_DevolveEstacionarComVersaoNaoSuportada()
    {
        var payloadComVDois = PayloadTradeRegisteredValido.Replace("\"v\": 1,", "\"v\": 2,");

        var desfecho = _roteador.Rotear("trades.registered", payloadComVDois);

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.VersaoNaoSuportada, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_TradesRegisteredComTipoDivergenteDeTradeRegistered_DevolveEstacionarComPayloadInvalido()
    {
        var payloadComTipoErrado = PayloadTradeRegisteredValido.Replace(
            "\"tipo\": \"TradeRegistered\",", "\"tipo\": \"OutraCoisa\",");

        var desfecho = _roteador.Rotear("trades.registered", payloadComTipoErrado);

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.PayloadInvalido, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_EodReady_DevolveEstacionarComTipoNaoTratadoEod()
    {
        var desfecho = _roteador.Rotear("eod.ready", "{\"d\":\"2026-08-01\"}");

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.TipoNaoTratadoEod, desfecho.Motivo);
    }

    [Theory]
    [InlineData("prices.td")]
    [InlineData("prices.asof")]
    public void Rotear_PricesQualquer_DevolveEstacionarComTipoNaoTratadoPrices(string routingKey)
    {
        var desfecho = _roteador.Rotear(routingKey, "{\"instrumentoId\":\"td:x\"}");

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.TipoNaoTratadoPrices, desfecho.Motivo);
    }

    private const string PayloadPriceObservedValido = """
        {
          "v": 1, "tipo": "PriceObserved",
          "instrumentoId": "td:tesouro-ipca-2035-05-15",
          "dataRef": "2026-08-01", "campo": "pu_venda",
          "valor": "3496.412345", "fonte": "td-api",
          "revisao": 0, "observadoEm": "2026-08-01T20:00:00Z"
        }
        """;

    [Fact]
    public void Rotear_PricesComPriceObservedValido_DevolveObservarComOEventoParseado()
    {
        var desfecho = _roteador.Rotear("prices.td", PayloadPriceObservedValido);

        Assert.Equal(DesfechoRoteamentoTipo.Observar, desfecho.Tipo);
        Assert.NotNull(desfecho.EventoPreco);
        Assert.Equal("td:tesouro-ipca-2035-05-15", desfecho.EventoPreco!.InstrumentoId);
    }

    [Fact]
    public void Rotear_PricesComPriceObservedComVersaoNaoSuportada_DevolveEstacionarComVersaoNaoSuportada()
    {
        var payload = PayloadPriceObservedValido.Replace("\"v\": 1,", "\"v\": 2,");

        var desfecho = _roteador.Rotear("prices.td", payload);

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.VersaoNaoSuportada, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_PricesComInstrumentoIdComEspacoNaBorda_DevolveEstacionarComIdentificadorComEspacoNaBorda()
    {
        var payload = PayloadPriceObservedValido.Replace(
            "\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",", "\"instrumentoId\": \" td:x\",");

        var desfecho = _roteador.Rotear("prices.td", payload);

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.IdentificadorComEspacoNaBorda, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_PricesComPriceObservedInvalido_DevolveEstacionarComPayloadInvalido()
    {
        var payload = PayloadPriceObservedValido.Replace("\"campo\": \"pu_venda\",", string.Empty);

        var desfecho = _roteador.Rotear("prices.td", payload);

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.PayloadInvalido, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_PricesComOutroTipoDeclarado_ContinuaTipoNaoTratadoPrices()
    {
        var payload = PayloadPriceObservedValido.Replace("\"tipo\": \"PriceObserved\",", "\"tipo\": \"CorporateActionObserved\",");

        var desfecho = _roteador.Rotear("prices.td", payload);

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.TipoNaoTratadoPrices, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_Corpactions_DevolveEstacionarComTipoNaoTratadoCorpactions()
    {
        var desfecho = _roteador.Rotear("corpactions.td", "{\"instrumentoId\":\"td:x\"}");

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.TipoNaoTratadoCorpactions, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_PricesSmokeComPayloadNaoJsonComPrefixoDeSonda_DevolveIgnorar()
    {
        var desfecho = _roteador.Rotear("prices.smoke", "custodia-f2-smoke-1234567890-999");

        Assert.Equal(DesfechoRoteamentoTipo.Ignorar, desfecho.Tipo);
    }

    [Fact]
    public void Rotear_PricesSmokeComPayloadJsonDeContrato_NaoEhTratadoComoSonda_ControlePositivo()
    {
        var desfecho = _roteador.Rotear("prices.smoke", "{\"instrumentoId\":\"td:x\",\"preco\":\"100.00\"}");

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.TipoNaoTratadoPrices, desfecho.Motivo);
    }

    [Fact]
    public void Rotear_PricesSmokeComPayloadNaoJsonSemPrefixoDeSonda_NaoEhIgnorado()
    {
        var desfecho = _roteador.Rotear("prices.smoke", "algum texto qualquer sem o prefixo esperado");

        Assert.Equal(DesfechoRoteamentoTipo.Estacionar, desfecho.Tipo);
        Assert.Equal(MotivoParking.TipoNaoTratadoPrices, desfecho.Motivo);
    }
}

using System.Text.Json;
using Custodia.Application.Eventos;

namespace Custodia.Application.Tests.Eventos;

public sealed class TradeRegisteredPayloadTests
{
    private const string PayloadAplicacaoCompleto = """
        {
          "v": 1,
          "tipo": "TradeRegistered",
          "tradeId": "op-7f3a",
          "clienteId": "cli-001",
          "instrumentoId": "td:tesouro-ipca-2035-05-15",
          "operacao": "aplicacao",
          "quantidade": "2.86000000",
          "valorFinanceiro": "10000.00",
          "dataEvento": "2026-08-01",
          "registradoEm": "2026-08-15T14:02:11Z",
          "valorOrigemSaldo": "900.00"
        }
        """;

    [Fact]
    public void Deserializar_PayloadCompletoDeAplicacao_MapeiaTodosOsCamposCorretamente()
    {
        var resultado = TradeRegisteredPayload.Deserializar(PayloadAplicacaoCompleto);

        Assert.True(resultado.IsSuccess);
        var evento = resultado.Value;

        Assert.Equal("op-7f3a", evento.TradeId);
        Assert.Equal("cli-001", evento.ClienteId);
        Assert.Equal("td:tesouro-ipca-2035-05-15", evento.InstrumentoId);
        Assert.Equal(OperacaoTrade.Aplicacao, evento.Operacao);
        Assert.Equal(2.86000000m, evento.Quantidade);
        Assert.Equal(10000.00m, evento.ValorFinanceiro);
        Assert.Equal(new DateOnly(2026, 8, 1), evento.DataEvento);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 14, 2, 11, TimeSpan.Zero), evento.RegistradoEm);
        Assert.Null(evento.EstornaTradeId);
        Assert.Equal("900.00", evento.ValorOrigemSaldoBruto);
    }

    [Fact]
    public void Deserializar_PayloadDeEstornoCompleto_CarregaEstornaTradeId()
    {
        const string payload = """
            {
              "v": 1, "tipo": "TradeRegistered",
              "tradeId": "op-3c9b", "clienteId": "cli-001",
              "instrumentoId": "td:tesouro-ipca-2035-05-15",
              "operacao": "estorno",
              "quantidade": "2.86000000", "valorFinanceiro": "10000.00",
              "dataEvento": "2026-08-01", "registradoEm": "2026-08-20T09:00:00Z",
              "estornaTradeId": "op-7f3a"
            }
            """;

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(OperacaoTrade.Estorno, resultado.Value.Operacao);
        Assert.Equal("op-7f3a", resultado.Value.EstornaTradeId);
        Assert.Null(resultado.Value.ValorOrigemSaldoBruto);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(99)]
    public void Deserializar_VersaoDiferenteDeUm_DevolveVersaoNaoSuportada(int versao)
    {
        var payload = PayloadComVCustomizado(versao.ToString());

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.VersaoNaoSuportada, resultado.Error);
    }

    [Fact]
    public void Deserializar_SemCampoV_DevolveVersaoNaoSuportada_NuncaParsingOtimista()
    {
        var payload = PayloadSemCampo("\"v\": 1,");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.VersaoNaoSuportada, resultado.Error);
    }

    [Theory]
    [InlineData("\"tradeId\": \"op-7f3a\",")]
    [InlineData("\"clienteId\": \"cli-001\",")]
    [InlineData("\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",")]
    [InlineData("\"operacao\": \"aplicacao\",")]
    [InlineData("\"quantidade\": \"2.86000000\",")]
    [InlineData("\"valorFinanceiro\": \"10000.00\",")]
    [InlineData("\"dataEvento\": \"2026-08-01\",")]
    [InlineData("\"registradoEm\": \"2026-08-15T14:02:11Z\",")]
    public void Deserializar_CampoObrigatorioAusente_DevolvePayloadInvalido(string trechoRemovido)
    {
        var payload = PayloadSemCampo(trechoRemovido);

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_TipoDivergenteDeTradeRegistered_DevolvePayloadInvalido()
    {
        var payload = PayloadComTipoCustomizado("PriceObserved");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_TipoAusente_DevolvePayloadInvalido()
    {
        var payload = PayloadSemCampo("\"tipo\": \"TradeRegistered\",");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_VersaoETipoAmbosDivergentes_DevolveVersaoNaoSuportada_VersaoTemPrecedencia()
    {
        var payload = PayloadComVCustomizado("2").Replace("\"tipo\": \"TradeRegistered\",", "\"tipo\": \"PriceObserved\",");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.VersaoNaoSuportada, resultado.Error);
    }

    [Fact]
    public void Deserializar_OperacaoDesconhecida_DevolvePayloadInvalido()
    {
        var payload = PayloadComOperacaoCustomizada("liquidacao_antecipada");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_EstornoSemEstornaTradeId_DevolvePayloadInvalido()
    {
        const string payload = """
            {
              "v": 1, "tipo": "TradeRegistered",
              "tradeId": "op-3c9b", "clienteId": "cli-001",
              "instrumentoId": "td:tesouro-ipca-2035-05-15",
              "operacao": "estorno",
              "quantidade": "2.86000000", "valorFinanceiro": "10000.00",
              "dataEvento": "2026-08-01", "registradoEm": "2026-08-20T09:00:00Z"
            }
            """;

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_QuantidadeComoNumeroJsonEmVezDeString_DevolvePayloadInvalido_NuncaZero()
    {
        const string payload = """
            {
              "v": 1, "tipo": "TradeRegistered",
              "tradeId": "op-7f3a", "clienteId": "cli-001",
              "instrumentoId": "td:tesouro-ipca-2035-05-15",
              "operacao": "aplicacao",
              "quantidade": 2.86, "valorFinanceiro": "10000.00",
              "dataEvento": "2026-08-01", "registradoEm": "2026-08-15T14:02:11Z"
            }
            """;

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_QuantidadeComTextoNaoDecimal_DevolvePayloadInvalido()
    {
        var payload = PayloadComQuantidadeCustomizada("\"não-é-um-decimal\"");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_DataEventoForaDoFormato_DevolvePayloadInvalido()
    {
        const string payload = """
            {
              "v": 1, "tipo": "TradeRegistered",
              "tradeId": "op-7f3a", "clienteId": "cli-001",
              "instrumentoId": "td:tesouro-ipca-2035-05-15",
              "operacao": "aplicacao",
              "quantidade": "2.86000000", "valorFinanceiro": "10000.00",
              "dataEvento": "01/08/2026", "registradoEm": "2026-08-15T14:02:11Z"
            }
            """;

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_ValorOrigemSaldoAusente_NaoEhTratadoComoZero()
    {
        const string payload = """
            {
              "v": 1, "tipo": "TradeRegistered",
              "tradeId": "op-7f3a", "clienteId": "cli-001",
              "instrumentoId": "td:tesouro-ipca-2035-05-15",
              "operacao": "aplicacao",
              "quantidade": "2.86000000", "valorFinanceiro": "10000.00",
              "dataEvento": "2026-08-01", "registradoEm": "2026-08-15T14:02:11Z"
            }
            """;

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value.ValorOrigemSaldoBruto);
    }

    [Fact]
    public void Deserializar_ValorOrigemSaldoComoNumeroJson_NaoEhAceitoComoDecimalValido()
    {
        const string payload = """
            {
              "v": 1, "tipo": "TradeRegistered",
              "tradeId": "op-7f3a", "clienteId": "cli-001",
              "instrumentoId": "td:tesouro-ipca-2035-05-15",
              "operacao": "aplicacao",
              "quantidade": "2.86000000", "valorFinanceiro": "10000.00",
              "dataEvento": "2026-08-01", "registradoEm": "2026-08-15T14:02:11Z",
              "valorOrigemSaldo": 900.00
            }
            """;

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value.ValorOrigemSaldoBruto);
        Assert.False(decimal.TryParse(
            resultado.Value.ValorOrigemSaldoBruto,
            System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture,
            out _));
    }

    [Fact]
    public void Deserializar_JsonSintaticamenteInvalido_PropagaJsonException_ChamadorTemQueTraduzirParaPayloadInvalido()
    {
        var excecao = Record.Exception(() => TradeRegisteredPayload.Deserializar("isto não é json"));

        Assert.IsAssignableFrom<JsonException>(excecao);
    }

    private static string PayloadSemCampo(string trecho) =>
        PayloadAplicacaoCompleto.Replace(trecho, string.Empty);

    private static string PayloadComVCustomizado(string valor) =>
        PayloadAplicacaoCompleto.Replace("\"v\": 1,", $"\"v\": {valor},");

    private static string PayloadComTipoCustomizado(string tipo) =>
        PayloadAplicacaoCompleto.Replace("\"tipo\": \"TradeRegistered\",", $"\"tipo\": \"{tipo}\",");

    private static string PayloadComOperacaoCustomizada(string operacao) =>
        PayloadAplicacaoCompleto.Replace("\"operacao\": \"aplicacao\",", $"\"operacao\": \"{operacao}\",");

    private static string PayloadComQuantidadeCustomizada(string quantidadeJson) =>
        PayloadAplicacaoCompleto.Replace("\"quantidade\": \"2.86000000\",", $"\"quantidade\": {quantidadeJson},");
}

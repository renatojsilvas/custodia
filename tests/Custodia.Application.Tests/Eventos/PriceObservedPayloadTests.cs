using Custodia.Application.Eventos;

namespace Custodia.Application.Tests.Eventos;

public sealed class PriceObservedPayloadTests
{
    private const string PayloadValido = """
        {
          "v": 1, "tipo": "PriceObserved",
          "instrumentoId": "td:tesouro-ipca-2035-05-15",
          "dataRef": "2026-08-01",
          "campo": "pu_venda",
          "valor": "3496.412345",
          "fonte": "td-api",
          "revisao": 0,
          "observadoEm": "2026-08-01T20:00:00Z",
          "extraIgnorado": true
        }
        """;

    [Fact]
    public void Deserializar_PayloadValido_MapeiaTodosOsCamposCorretamente()
    {
        var resultado = PriceObservedPayload.Deserializar(PayloadValido);

        Assert.True(resultado.IsSuccess);
        var evento = resultado.Value;

        Assert.Equal("td:tesouro-ipca-2035-05-15", evento.InstrumentoId);
        Assert.Equal(new DateOnly(2026, 8, 1), evento.DataRef);
        Assert.Equal("pu_venda", evento.Campo);
        Assert.Equal(3496.412345m, evento.Valor);
        Assert.Equal("td-api", evento.Fonte);
        Assert.Equal(0, evento.Revisao);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero), evento.ObservadoEm);
    }

    [Fact]
    public void Deserializar_ComRevisaoMaiorQueZero_MapeiaARevisao()
    {
        var payload = PayloadValido.Replace("\"revisao\": 0,", "\"revisao\": 3,");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(3, resultado.Value.Revisao);
    }

    [Fact]
    public void Deserializar_VersaoDiferenteDeUm_DevolveVersaoNaoSuportada()
    {
        var payload = PayloadValido.Replace("\"v\": 1,", "\"v\": 2,");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.VersaoNaoSuportada, resultado.Error);
    }

    [Fact]
    public void Deserializar_TipoDivergenteDePriceObserved_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"tipo\": \"PriceObserved\",", "\"tipo\": \"OutraCoisa\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Theory]
    [InlineData("\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",")]
    [InlineData("\"dataRef\": \"2026-08-01\",")]
    [InlineData("\"campo\": \"pu_venda\",")]
    [InlineData("\"valor\": \"3496.412345\",")]
    [InlineData("\"fonte\": \"td-api\",")]
    [InlineData("\"observadoEm\": \"2026-08-01T20:00:00Z\",")]
    public void Deserializar_SemCampoObrigatorio_DevolvePayloadInvalido(string trechoAremover)
    {
        var payload = PayloadValido.Replace(trechoAremover, string.Empty);

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_RevisaoNegativa_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"revisao\": 0,", "\"revisao\": -1,");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_InstrumentoNoNamespaceCaixa_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace(
            "\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",", "\"instrumentoId\": \"caixa:BRL\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Theory]
    [InlineData("caixa:BRL")]
    [InlineData("CAIXA:BRL")]
    [InlineData("Caixa:a_liquidar")]
    public void Deserializar_InstrumentoNoNamespaceCaixaEmQualquerCaixa_DevolvePayloadInvalido(string instrumentoId)
    {
        var payload = PayloadValido.Replace(
            "\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",", $"\"instrumentoId\": \"{instrumentoId}\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Theory]
    [InlineData(" caixa:BRL")]
    [InlineData("caixa:BRL ")]
    [InlineData("\\tcaixa:BRL")]
    [InlineData(" td:x")]
    [InlineData("td:x\\t")]
    public void Deserializar_InstrumentoIdComEspacoNaBorda_DevolveIdentificadorComEspacoNaBorda(string instrumentoId)
    {
        var payload = PayloadValido.Replace(
            "\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",", $"\"instrumentoId\": \"{instrumentoId}\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.IdentificadorComEspacoNaBorda, resultado.Error);
    }

    [Theory]
    [InlineData(" pu_venda")]
    [InlineData("pu_venda ")]
    [InlineData("pu_venda\\t")]
    public void Deserializar_CampoComEspacoNaBorda_DevolveIdentificadorComEspacoNaBorda(string campo)
    {
        var payload = PayloadValido.Replace("\"campo\": \"pu_venda\",", $"\"campo\": \"{campo}\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.IdentificadorComEspacoNaBorda, resultado.Error);
    }

    [Theory]
    [InlineData(" td-api")]
    [InlineData("td-api ")]
    [InlineData("td-api\\t")]
    public void Deserializar_FonteComEspacoNaBorda_DevolveIdentificadorComEspacoNaBorda(string fonte)
    {
        var payload = PayloadValido.Replace("\"fonte\": \"td-api\",", $"\"fonte\": \"{fonte}\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.IdentificadorComEspacoNaBorda, resultado.Error);
    }

    [Fact]
    public void Deserializar_InstrumentoIdSoEspacoEmBranco_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace(
            "\"instrumentoId\": \"td:tesouro-ipca-2035-05-15\",", "\"instrumentoId\": \"   \",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_CampoSoEspacoEmBranco_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"campo\": \"pu_venda\",", "\"campo\": \"   \",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_FonteSoEspacoEmBranco_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"fonte\": \"td-api\",", "\"fonte\": \"   \",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_InstrumentoIdCampoFonteSemEspacoNaBordaENaoCaixa_ControlePositivo_Aceita()
    {
        var resultado = PriceObservedPayload.Deserializar(PayloadValido);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("td:tesouro-ipca-2035-05-15", resultado.Value.InstrumentoId);
        Assert.Equal("pu_venda", resultado.Value.Campo);
        Assert.Equal("td-api", resultado.Value.Fonte);
    }

    [Fact]
    public void Deserializar_ValorComEscalaAcimaDaSuportada_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"valor\": \"3496.412345\",", "\"valor\": \"3496.4123456\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_ValorComMagnitudeAcimaDaSuportada_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"valor\": \"3496.412345\",", "\"valor\": \"1000000000000.000000\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_ValorNoLimiteSuperiorSuportado_ControlePositivo_Aceita()
    {
        var payload = PayloadValido.Replace("\"valor\": \"3496.412345\",", "\"valor\": \"999999999999.999999\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(999999999999.999999m, resultado.Value.Valor);
    }

    [Fact]
    public void Deserializar_DataRefEmFormatoInvalido_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"dataRef\": \"2026-08-01\",", "\"dataRef\": \"01/08/2026\",");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void Deserializar_ObservadoEmEmFormatoInvalido_DevolvePayloadInvalido()
    {
        var payload = PayloadValido.Replace("\"observadoEm\": \"2026-08-01T20:00:00Z\"", "\"observadoEm\": \"nao-eh-uma-data\"");

        var resultado = PriceObservedPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(PriceObservedErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public void CorrespondeAoTipo_PayloadComTipoPriceObserved_DevolveTrue()
    {
        Assert.True(PriceObservedPayload.CorrespondeAoTipo(PayloadValido));
    }

    [Fact]
    public void CorrespondeAoTipo_PayloadComOutroTipo_DevolveFalse()
    {
        var payload = PayloadValido.Replace("\"tipo\": \"PriceObserved\",", "\"tipo\": \"OutraCoisa\",");

        Assert.False(PriceObservedPayload.CorrespondeAoTipo(payload));
    }

    [Fact]
    public void CorrespondeAoTipo_PayloadSemTipo_DevolveFalse()
    {
        Assert.False(PriceObservedPayload.CorrespondeAoTipo("{\"v\":1}"));
    }
}

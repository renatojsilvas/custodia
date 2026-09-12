using System.Globalization;
using System.Text.Json;
using Custodia.Domain.Common;

namespace Custodia.Application.Eventos;

public static class TradeRegisteredPayload
{
    public const string Tipo = "TradeRegistered";
    public const string RoutingKey = "trades.registered";

    private const int VersaoSuportada = 1;
    private const string FormatoDataEvento = "yyyy-MM-dd";
    private const string ValorOrigemSaldoTipoInvalido = "#tipo-json-invalido#";

    public static Result<TradeRegisteredEvento> Deserializar(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        var raiz = documento.RootElement;

        if (raiz.ValueKind != JsonValueKind.Object)
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!TentaExtrairInteiro(raiz, "v", out var versao) || versao != VersaoSuportada)
        {
            return TradeRegisteredErrors.VersaoNaoSuportada;
        }

        if (!TentaExtrairTextoObrigatorio(raiz, "tipo", out var tipo) || tipo != Tipo)
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!TentaExtrairTextoObrigatorio(raiz, "tradeId", out var tradeId)
            || !TentaExtrairTextoObrigatorio(raiz, "clienteId", out var clienteId)
            || !TentaExtrairTextoObrigatorio(raiz, "instrumentoId", out var instrumentoId)
            || !TentaExtrairTextoObrigatorio(raiz, "operacao", out var operacaoBruta)
            || !TentaExtrairTextoObrigatorio(raiz, "quantidade", out var quantidadeBruta)
            || !TentaExtrairTextoObrigatorio(raiz, "valorFinanceiro", out var valorFinanceiroBruta)
            || !TentaExtrairTextoObrigatorio(raiz, "dataEvento", out var dataEventoBruta)
            || !TentaExtrairTextoObrigatorio(raiz, "registradoEm", out var registradoEmBruta))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!OperacaoTradeVocabulario.TryFromName(operacaoBruta, out var operacao))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!TentaExtrairTextoOpcional(raiz, "estornaTradeId", out var estornaTradeId))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (operacao == OperacaoTrade.Estorno && string.IsNullOrEmpty(estornaTradeId))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!DecimalContrato.TryParse(quantidadeBruta, out var quantidade))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!DecimalContrato.TryParse(valorFinanceiroBruta, out var valorFinanceiro))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!DateOnly.TryParseExact(
                dataEventoBruta, FormatoDataEvento, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataEvento))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!DateTimeOffset.TryParse(
                registradoEmBruta,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var registradoEm))
        {
            return TradeRegisteredErrors.PayloadInvalido;
        }

        if (!TentaExtrairTextoOpcional(raiz, "valorOrigemSaldo", out var valorOrigemSaldoBruto))
        {
            valorOrigemSaldoBruto = ValorOrigemSaldoTipoInvalido;
        }

        return new TradeRegisteredEvento(
            tradeId,
            clienteId,
            instrumentoId,
            operacao,
            quantidade,
            valorFinanceiro,
            dataEvento,
            registradoEm,
            estornaTradeId,
            valorOrigemSaldoBruto);
    }

    private static bool TentaExtrairInteiro(JsonElement raiz, string nome, out int valor)
    {
        if (raiz.TryGetProperty(nome, out var propriedade)
            && propriedade.ValueKind == JsonValueKind.Number
            && propriedade.TryGetInt32(out valor))
        {
            return true;
        }

        valor = default;
        return false;
    }

    private static bool TentaExtrairTextoObrigatorio(JsonElement raiz, string nome, out string valor)
    {
        if (raiz.TryGetProperty(nome, out var propriedade)
            && propriedade.ValueKind == JsonValueKind.String)
        {
            var texto = propriedade.GetString();
            if (!string.IsNullOrEmpty(texto))
            {
                valor = texto;
                return true;
            }
        }

        valor = string.Empty;
        return false;
    }

    private static bool TentaExtrairTextoOpcional(JsonElement raiz, string nome, out string? valor)
    {
        if (!raiz.TryGetProperty(nome, out var propriedade)
            || propriedade.ValueKind == JsonValueKind.Null
            || propriedade.ValueKind == JsonValueKind.Undefined)
        {
            valor = null;
            return true;
        }

        if (propriedade.ValueKind != JsonValueKind.String)
        {
            valor = null;
            return false;
        }

        var texto = propriedade.GetString();
        valor = string.IsNullOrEmpty(texto) ? null : texto;
        return true;
    }
}

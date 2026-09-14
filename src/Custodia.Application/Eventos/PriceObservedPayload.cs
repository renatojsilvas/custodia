using System.Globalization;
using System.Text.Json;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Application.Eventos;

public static class PriceObservedPayload
{
    public const string Tipo = "PriceObserved";

    private const int VersaoSuportada = 1;
    private const string FormatoDataRef = "yyyy-MM-dd";

    public static bool CorrespondeAoTipo(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        var raiz = documento.RootElement;

        return raiz.ValueKind == JsonValueKind.Object
            && raiz.TryGetProperty("tipo", out var propriedade)
            && propriedade.ValueKind == JsonValueKind.String
            && propriedade.GetString() == Tipo;
    }

    public static Result<PriceObservedEvento> Deserializar(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        var raiz = documento.RootElement;

        if (raiz.ValueKind != JsonValueKind.Object)
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (!TentaExtrairInteiro(raiz, "v", out var versao) || versao != VersaoSuportada)
        {
            return PriceObservedErrors.VersaoNaoSuportada;
        }

        if (!TentaExtrairTextoObrigatorio(raiz, "tipo", out var tipo) || tipo != Tipo)
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (!TentaExtrairTextoObrigatorio(raiz, "instrumentoId", out var instrumentoId)
            || !TentaExtrairTextoObrigatorio(raiz, "dataRef", out var dataRefBruta)
            || !TentaExtrairTextoObrigatorio(raiz, "campo", out var campo)
            || !TentaExtrairTextoObrigatorio(raiz, "valor", out var valorBruto)
            || !TentaExtrairTextoObrigatorio(raiz, "fonte", out var fonte)
            || !TentaExtrairTextoObrigatorio(raiz, "observadoEm", out var observadoEmBruta))
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (!TentaExtrairInteiro(raiz, "revisao", out var revisao) || revisao < 0)
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (instrumentoId.StartsWith(InstrumentosCaixa.Prefixo, StringComparison.OrdinalIgnoreCase))
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (!DecimalContrato.TryParse(valorBruto, out var valor) || ExcedePrecisaoSuportada(valor))
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (!DateOnly.TryParseExact(
                dataRefBruta, FormatoDataRef, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataRef))
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        if (!DateTimeOffset.TryParse(
                observadoEmBruta,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var observadoEm))
        {
            return PriceObservedErrors.PayloadInvalido;
        }

        return new PriceObservedEvento(instrumentoId, dataRef, campo, valor, fonte, revisao, observadoEm);
    }

    private static bool ExcedePrecisaoSuportada(decimal valor) =>
        Math.Abs(valor) >= SchemaNumericLimits.PrecoLimiteSuperiorExclusivo
        || decimal.Round(valor, SchemaNumericLimits.PrecoEscala) != valor;

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
}

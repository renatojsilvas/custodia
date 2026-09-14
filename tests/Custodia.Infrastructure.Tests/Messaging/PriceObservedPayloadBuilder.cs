using System.Globalization;
using System.Text;

namespace Custodia.Infrastructure.Tests.Messaging;

internal static class PriceObservedPayloadBuilder
{
    public static string Valido(
        string instrumentoId,
        DateOnly dataRef,
        string campo,
        decimal valor,
        string fonte,
        int revisao,
        DateTimeOffset observadoEm)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"v\":1,");
        builder.Append("\"tipo\":\"PriceObserved\",");
        AppendString(builder, "instrumentoId", instrumentoId);
        AppendString(builder, "dataRef", dataRef.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendString(builder, "campo", campo);
        AppendString(builder, "valor", valor.ToString("F6", CultureInfo.InvariantCulture));
        AppendString(builder, "fonte", fonte);
        builder.Append("\"revisao\":").Append(revisao.ToString(CultureInfo.InvariantCulture)).Append(',');
        AppendString(
            builder,
            "observadoEm",
            observadoEm.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ultimo: true);
        builder.Append('}');
        return builder.ToString();
    }

    public static string ComVersaoNaoSuportada(string instrumentoId)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"v\":2,");
        builder.Append("\"tipo\":\"PriceObserved\",");
        AppendString(builder, "instrumentoId", instrumentoId);
        AppendString(builder, "dataRef", "2026-08-01");
        AppendString(builder, "campo", "pu_venda");
        AppendString(builder, "valor", "100.000000");
        AppendString(builder, "fonte", "td-api");
        builder.Append("\"revisao\":0,");
        AppendString(builder, "observadoEm", "2026-08-01T20:00:00Z", ultimo: true);
        builder.Append('}');
        return builder.ToString();
    }

    public static string ComOutroTipo(string instrumentoId)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"v\":1,");
        builder.Append("\"tipo\":\"CorporateActionObserved\",");
        AppendString(builder, "instrumentoId", instrumentoId, ultimo: true);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendString(StringBuilder builder, string nome, string valor, bool ultimo = false)
    {
        builder.Append('"').Append(nome).Append("\":\"").Append(valor).Append('"');
        if (!ultimo)
        {
            builder.Append(',');
        }
    }
}

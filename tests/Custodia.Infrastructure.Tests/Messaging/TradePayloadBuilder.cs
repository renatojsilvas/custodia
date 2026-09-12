using System.Globalization;
using System.Text;

namespace Custodia.Infrastructure.Tests.Messaging;

internal static class TradePayloadBuilder
{
    public static string Aplicacao(
        string tradeId,
        string clienteId,
        string instrumentoId,
        decimal quantidade,
        decimal valorFinanceiro,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        decimal? valorOrigemSaldo = 0m) =>
        Construir("aplicacao", tradeId, clienteId, instrumentoId, quantidade, valorFinanceiro, dataEvento, registradoEm, null, valorOrigemSaldo);

    public static string Estorno(
        string tradeId,
        string clienteId,
        string instrumentoId,
        decimal quantidade,
        decimal valorFinanceiro,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        string estornaTradeId) =>
        Construir("estorno", tradeId, clienteId, instrumentoId, quantidade, valorFinanceiro, dataEvento, registradoEm, estornaTradeId, null);

    public static string ComVersaoNaoSuportada(string tradeId, string clienteId, string instrumentoId)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"v\":2,");
        builder.Append("\"tipo\":\"TradeRegistered\",");
        builder.Append('"').Append("tradeId").Append("\":\"").Append(tradeId).Append("\",");
        builder.Append('"').Append("clienteId").Append("\":\"").Append(clienteId).Append("\",");
        builder.Append('"').Append("instrumentoId").Append("\":\"").Append(instrumentoId).Append("\",");
        builder.Append("\"operacao\":\"aplicacao\",");
        builder.Append("\"quantidade\":\"1.00000000\",");
        builder.Append("\"valorFinanceiro\":\"100.00\",");
        builder.Append("\"dataEvento\":\"2026-08-01\",");
        builder.Append("\"registradoEm\":\"2026-08-15T14:02:11Z\"");
        builder.Append('}');
        return builder.ToString();
    }

    private static string Construir(
        string operacao,
        string tradeId,
        string clienteId,
        string instrumentoId,
        decimal quantidade,
        decimal valorFinanceiro,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        string? estornaTradeId,
        decimal? valorOrigemSaldo)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"v\":1,");
        builder.Append("\"tipo\":\"TradeRegistered\",");
        AppendString(builder, "tradeId", tradeId);
        AppendString(builder, "clienteId", clienteId);
        AppendString(builder, "instrumentoId", instrumentoId);
        AppendString(builder, "operacao", operacao);
        AppendString(builder, "quantidade", quantidade.ToString("F8", CultureInfo.InvariantCulture));
        AppendString(builder, "valorFinanceiro", valorFinanceiro.ToString("F2", CultureInfo.InvariantCulture));
        AppendString(builder, "dataEvento", dataEvento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var registradoEmTexto = registradoEm.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        if (estornaTradeId is null && valorOrigemSaldo is null)
        {
            AppendString(builder, "registradoEm", registradoEmTexto, ultimo: true);
        }
        else
        {
            AppendString(builder, "registradoEm", registradoEmTexto);

            if (estornaTradeId is not null && valorOrigemSaldo is not null)
            {
                AppendString(builder, "estornaTradeId", estornaTradeId);
                AppendString(builder, "valorOrigemSaldo", valorOrigemSaldo.Value.ToString("F2", CultureInfo.InvariantCulture), ultimo: true);
            }
            else if (estornaTradeId is not null)
            {
                AppendString(builder, "estornaTradeId", estornaTradeId, ultimo: true);
            }
            else
            {
                AppendString(builder, "valorOrigemSaldo", valorOrigemSaldo!.Value.ToString("F2", CultureInfo.InvariantCulture), ultimo: true);
            }
        }

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

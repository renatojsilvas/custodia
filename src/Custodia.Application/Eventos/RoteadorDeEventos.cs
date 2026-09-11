using Custodia.Domain.Eventos;

namespace Custodia.Application.Eventos;

public sealed class RoteadorDeEventos
{
    private const string RoutingKeyTradeRegistered = "trades.registered";
    private const string RoutingKeyEodReady = "eod.ready";
    private const string RoutingKeySmoke = "prices.smoke";
    private const string PrefixoPrices = "prices.";
    private const string PrefixoCorpactions = "corpactions.";
    private const string PrefixoSondaDeDeploy = "custodia-f2-";

    public DesfechoRoteamento Rotear(string routingKey, string corpo, IReadOnlyDictionary<string, object>? cabecalhos = null)
    {
        ArgumentNullException.ThrowIfNull(routingKey);
        ArgumentNullException.ThrowIfNull(corpo);

        if (routingKey == RoutingKeyTradeRegistered)
        {
            return RotearTradeRegistered(corpo);
        }

        if (routingKey == RoutingKeyEodReady)
        {
            return DesfechoRoteamento.Estacionar(MotivoEstacionamento.TipoNaoTratadoEod);
        }

        if (routingKey == RoutingKeySmoke && EhSondaDeDeploy(corpo))
        {
            return DesfechoRoteamento.Ignorar();
        }

        if (routingKey.StartsWith(PrefixoPrices, StringComparison.Ordinal))
        {
            return DesfechoRoteamento.Estacionar(MotivoEstacionamento.TipoNaoTratadoPrices);
        }

        if (routingKey.StartsWith(PrefixoCorpactions, StringComparison.Ordinal))
        {
            return DesfechoRoteamento.Estacionar(MotivoEstacionamento.TipoNaoTratadoCorpactions);
        }

        return DesfechoRoteamento.Estacionar(MotivoEstacionamento.PayloadInvalido);
    }

    private static DesfechoRoteamento RotearTradeRegistered(string corpo)
    {
        var resultado = TradeRegisteredPayload.Deserializar(corpo);

        if (resultado.IsSuccess)
        {
            return DesfechoRoteamento.Escriturar(resultado.Value);
        }

        var motivo = resultado.Error == TradeRegisteredErrors.VersaoNaoSuportada
            ? MotivoEstacionamento.VersaoNaoSuportada
            : MotivoEstacionamento.PayloadInvalido;

        return DesfechoRoteamento.Estacionar(motivo);
    }

    private static bool EhSondaDeDeploy(string corpo) =>
        !EhJson(corpo) && corpo.StartsWith(PrefixoSondaDeDeploy, StringComparison.Ordinal);

    private static bool EhJson(string corpo)
    {
        var aparado = corpo.AsSpan().TrimStart();
        return aparado.Length > 0 && aparado[0] == '{';
    }
}

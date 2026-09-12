using Custodia.Infrastructure.Messaging;

namespace Custodia.Infrastructure.Tests.Messaging;

internal sealed class PublicadorComFalhaForcada(
    IPublicadorComConfirmacao interno, Func<string, int, bool>? confirmar = null) : IPublicadorComConfirmacao
{
    private readonly Func<string, int, bool> _confirmar = confirmar ?? ((_, _) => true);
    private readonly Dictionary<string, int> _tentativasPorExchange = [];
    private readonly object _cadeado = new();

    public List<(string Exchange, string RoutingKey, int Tentativa, Dictionary<string, object?> Cabecalhos)> Chamadas { get; } = [];

    public Task<bool> PublicarAsync(
        string exchange, string routingKey, IDictionary<string, object?> cabecalhos, ReadOnlyMemory<byte> corpo, CancellationToken ct)
    {
        int tentativa;
        lock (_cadeado)
        {
            tentativa = _tentativasPorExchange.TryGetValue(exchange, out var atual) ? atual + 1 : 1;
            _tentativasPorExchange[exchange] = tentativa;
            Chamadas.Add((exchange, routingKey, tentativa, new Dictionary<string, object?>(cabecalhos)));
        }

        return _confirmar(exchange, tentativa)
            ? interno.PublicarAsync(exchange, routingKey, cabecalhos, corpo, ct)
            : Task.FromResult(false);
    }
}

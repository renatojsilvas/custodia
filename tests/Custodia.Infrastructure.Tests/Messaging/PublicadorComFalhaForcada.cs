using Custodia.Infrastructure.Messaging;

namespace Custodia.Infrastructure.Tests.Messaging;

internal sealed class PublicadorComFalhaForcada(
    IPublicadorComConfirmacao interno,
    Func<string, int, bool>? confirmar = null,
    Func<string, int, bool>? entregaMesmoComConfirmNegado = null,
    Func<string, int, Exception?>? lancarExcecaoAoPublicar = null) : IPublicadorComConfirmacao
{
    private readonly Func<string, int, bool> _confirmar = confirmar ?? ((_, _) => true);
    private readonly Func<string, int, bool> _entregaMesmoComConfirmNegado = entregaMesmoComConfirmNegado ?? ((_, _) => false);
    private readonly Func<string, int, Exception?> _lancarExcecaoAoPublicar = lancarExcecaoAoPublicar ?? ((_, _) => null);
    private readonly Dictionary<string, int> _tentativasPorExchange = [];
    private readonly List<(string Exchange, string RoutingKey, int Tentativa, Dictionary<string, object?> Cabecalhos)> _chamadas = [];
    private readonly object _cadeado = new();

    public IReadOnlyList<(string Exchange, string RoutingKey, int Tentativa, Dictionary<string, object?> Cabecalhos)> Chamadas
    {
        get
        {
            lock (_cadeado)
            {
                return _chamadas.ToList();
            }
        }
    }

    public async Task<bool> PublicarAsync(
        string exchange, string routingKey, IDictionary<string, object?> cabecalhos, ReadOnlyMemory<byte> corpo, CancellationToken ct)
    {
        int tentativa;
        lock (_cadeado)
        {
            tentativa = _tentativasPorExchange.TryGetValue(exchange, out var atual) ? atual + 1 : 1;
            _tentativasPorExchange[exchange] = tentativa;
            _chamadas.Add((exchange, routingKey, tentativa, new Dictionary<string, object?>(cabecalhos)));
        }

        var excecaoForcada = _lancarExcecaoAoPublicar(exchange, tentativa);
        if (excecaoForcada is not null)
        {
            throw excecaoForcada;
        }

        if (_confirmar(exchange, tentativa))
        {
            return await interno.PublicarAsync(exchange, routingKey, cabecalhos, corpo, ct);
        }

        if (_entregaMesmoComConfirmNegado(exchange, tentativa))
        {
            await interno.PublicarAsync(exchange, routingKey, cabecalhos, corpo, ct);
        }

        return false;
    }
}

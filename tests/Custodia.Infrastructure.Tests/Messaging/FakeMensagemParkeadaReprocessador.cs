using Custodia.Domain.Eventos;
using Custodia.Infrastructure.Messaging;

namespace Custodia.Infrastructure.Tests.Messaging;

internal sealed class FakeMensagemParkeadaReprocessador(
    Func<string, ReadOnlyMemory<byte>, ResultadoReprocessamento>? reprocessar = null)
    : IMensagemParkeadaReprocessador
{
    private readonly Func<string, ReadOnlyMemory<byte>, ResultadoReprocessamento> _reprocessar =
        reprocessar ?? ((_, _) => ResultadoReprocessamento.Sucesso());

    public List<string> RoutingKeysReprocessadas { get; } = [];

    public Task<ResultadoReprocessamento> ReprocessarAsync(
        string routingKey, ReadOnlyMemory<byte> corpo, MotivoEstacionamento motivoOriginal, CancellationToken ct)
    {
        RoutingKeysReprocessadas.Add(routingKey);
        return Task.FromResult(_reprocessar(routingKey, corpo));
    }
}

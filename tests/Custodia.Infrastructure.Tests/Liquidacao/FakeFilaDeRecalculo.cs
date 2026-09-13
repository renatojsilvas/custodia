using System.Collections.Concurrent;
using Custodia.Application.Liquidacao;

namespace Custodia.Infrastructure.Tests.Liquidacao;

internal sealed record ChamadaDeRecalculo(string ClienteId, string InstrumentoId, DateOnly Desde);

internal sealed class FakeFilaDeRecalculo : IFilaDeRecalculo
{
    public ConcurrentBag<ChamadaDeRecalculo> Chamadas { get; } = [];

    public Task EnfileirarAsync(string clienteId, string instrumentoId, DateOnly desde, CancellationToken ct)
    {
        Chamadas.Add(new ChamadaDeRecalculo(clienteId, instrumentoId, desde));
        return Task.CompletedTask;
    }
}

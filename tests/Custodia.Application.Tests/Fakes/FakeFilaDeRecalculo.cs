using Custodia.Application.Liquidacao;

namespace Custodia.Application.Tests.Fakes;

internal sealed record ChamadaDeRecalculo(string ClienteId, string InstrumentoId, DateOnly Desde);

internal sealed class FakeFilaDeRecalculo : IFilaDeRecalculo
{
    public List<ChamadaDeRecalculo> Chamadas { get; } = [];

    public Task EnfileirarAsync(string clienteId, string instrumentoId, DateOnly desde, CancellationToken ct)
    {
        Chamadas.Add(new ChamadaDeRecalculo(clienteId, instrumentoId, desde));
        return Task.CompletedTask;
    }
}

using Custodia.Application.Precos.Hub;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed record ChamadaDeFatia(DateOnly Data, IReadOnlyList<string> Instrumentos);

internal sealed class FakeHubPrecosClient(
    Func<DateOnly, IReadOnlyList<string>, Result<IReadOnlyList<PrecoAsOfItem>>>? obterFatia = null)
    : IHubPrecosClient
{
    private readonly Func<DateOnly, IReadOnlyList<string>, Result<IReadOnlyList<PrecoAsOfItem>>> _obterFatia =
        obterFatia ?? ((_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(Array.Empty<PrecoAsOfItem>()));

    public List<ChamadaDeFatia> Chamadas { get; } = [];

    public Task<Result<IReadOnlyList<PrecoAsOfItem>>> ObterFatiaAsync(
        DateOnly data, IReadOnlyList<string> instrumentos, CancellationToken ct)
    {
        Chamadas.Add(new ChamadaDeFatia(data, instrumentos));
        return Task.FromResult(_obterFatia(data, instrumentos));
    }
}

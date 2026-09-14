using Custodia.Domain.Common;

namespace Custodia.Application.Precos.Hub;

public interface IHubPrecosClient
{
    Task<Result<IReadOnlyList<PrecoAsOfItem>>> ObterFatiaAsync(
        DateOnly data, IReadOnlyList<string> instrumentos, CancellationToken ct);
}

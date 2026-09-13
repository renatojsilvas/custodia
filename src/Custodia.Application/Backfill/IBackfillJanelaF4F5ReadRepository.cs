using Custodia.Domain.Common;

namespace Custodia.Application.Backfill;

public interface IBackfillJanelaF4F5ReadRepository
{
    Task<Result<IReadOnlyList<ResgateSemAliq>>> ObterResgatesSemAliqAsync(CancellationToken ct);

    Task<Result<IReadOnlyList<AjusteDeResgateSemReversao>>> ObterAjustesDeResgateSemReversaoAsync(CancellationToken ct);
}

using Custodia.Domain.Common;

namespace Custodia.Application.Reparo;

public interface IRepararResgatesAntigosReadRepository
{
    Task<Result<IReadOnlyList<ResgateSemAliq>>> ObterResgatesSemAliqAsync(CancellationToken ct);

    Task<Result<IReadOnlyList<AjusteDeResgateSemReversao>>> ObterAjustesDeResgateSemReversaoAsync(CancellationToken ct);
}

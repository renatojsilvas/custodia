using Custodia.Domain.Common;

namespace Custodia.Application.Conciliacao;

public interface IConciliacaoDeResgatesReadRepository
{
    Task<Result<IReadOnlyList<ResgateTributado>>> ObterResgatesTributadosAsync(CancellationToken ct);
}

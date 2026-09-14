using Custodia.Domain.Common;

namespace Custodia.Application.Precos.Bootstrap;

public interface IEscopoDePrecosReadRepository
{
    Task<Result<EscopoLivroInteiroConsulta>> ObterLivroInteiroAsync(CancellationToken ct);

    Task<Result<IReadOnlyList<string>>> ObterSemPrecoAtualAsync(CancellationToken ct);
}

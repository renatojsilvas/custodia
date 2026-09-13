using Custodia.Domain.Common;

namespace Custodia.Application.Liquidacao;

public interface IAReceberVencidoReadRepository
{
    Task<Result<IReadOnlyList<AReceberVencido>>> ObterAbertasNaoRevertidasAsync(CancellationToken ct);
}

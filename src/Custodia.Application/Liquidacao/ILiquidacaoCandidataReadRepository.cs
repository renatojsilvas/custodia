using Custodia.Domain.Common;

namespace Custodia.Application.Liquidacao;

public interface ILiquidacaoCandidataReadRepository
{
    Task<Result<IReadOnlyList<LiquidacaoCandidata>>> ObterAbertasNaoRevertidasAsync(CancellationToken ct);
}

using Custodia.Application.Liquidacao;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeLiquidacaoCandidataReadRepository(IReadOnlyList<LiquidacaoCandidata> candidatas)
    : ILiquidacaoCandidataReadRepository
{
    public Task<Result<IReadOnlyList<LiquidacaoCandidata>>> ObterAbertasNaoRevertidasAsync(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<LiquidacaoCandidata>>.Success(candidatas));
}

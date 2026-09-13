using Custodia.Application.Liquidacao;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeAReceberVencidoReadRepository(IReadOnlyList<AReceberVencido> candidatas)
    : IAReceberVencidoReadRepository
{
    public Task<Result<IReadOnlyList<AReceberVencido>>> ObterAbertasNaoRevertidasAsync(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<AReceberVencido>>.Success(candidatas));
}

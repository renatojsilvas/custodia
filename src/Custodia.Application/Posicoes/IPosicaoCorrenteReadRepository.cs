using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public interface IPosicaoCorrenteReadRepository
{
    Task<Result<PosicaoTresColunas>> ObterAsync(string clienteId, string instrumentoId, CancellationToken ct);

    Task<Result<IReadOnlyList<ChavePosicao>>> ObterChavesAsync(
        string? clienteId, string? instrumentoId, CancellationToken ct);
}

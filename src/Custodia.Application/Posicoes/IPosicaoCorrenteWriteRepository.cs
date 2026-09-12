using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public interface IPosicaoCorrenteWriteRepository
{
    Task<Result> AtualizarAsync(string clienteId, string instrumentoId, PosicaoTresColunas estado, CancellationToken ct);

    Task<Result> RemoverAsync(string clienteId, string instrumentoId, CancellationToken ct);
}

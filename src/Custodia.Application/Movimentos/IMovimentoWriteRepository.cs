using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Application.Movimentos;

public interface IMovimentoWriteRepository
{
    Task<Result> AdicionarAsync(Movimento movimento, CancellationToken ct);
}

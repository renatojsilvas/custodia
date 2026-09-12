using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class MovimentoWriteRepository(AppDbContext dbContext) : IMovimentoWriteRepository
{
    public async Task<Result> AdicionarAsync(Movimento movimento, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);
        await dbContext.Movimentos.AddAsync(movimento, ct);
        return Result.Success();
    }
}

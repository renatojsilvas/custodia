using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Microsoft.EntityFrameworkCore;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class MovimentoTravamentoRepository(AppDbContext dbContext) : IMovimentoTravamentoRepository
{
    public async Task<Result<MovimentoConsulta>> TravarPorClienteERefExternaAsync(
        string clienteId, string refExterna, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        var linhas = await dbContext.Movimentos
            .FromSqlInterpolated(
                $"""
                SELECT * FROM movimentos
                WHERE cliente_id = {clienteId} AND ref_externa = {refExterna}
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        var linha = linhas.SingleOrDefault();

        return Result<MovimentoConsulta>.Success(
            linha is null ? MovimentoConsulta.NaoEncontrado : MovimentoConsulta.DeLinha(linha));
    }
}

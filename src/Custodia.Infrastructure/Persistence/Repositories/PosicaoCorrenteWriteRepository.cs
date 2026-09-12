using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class PosicaoCorrenteWriteRepository(AppDbContext dbContext) : IPosicaoCorrenteWriteRepository
{
    public async Task<Result> AtualizarAsync(
        string clienteId, string instrumentoId, PosicaoTresColunas estado, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO posicao_corrente (cliente_id, instrumento_id, quantidade, preco_medio, custo_total)
                VALUES ({clienteId}, {instrumentoId}, {estado.Quantidade}, {estado.PrecoMedio}, {estado.CustoTotal})
                ON CONFLICT (cliente_id, instrumento_id) DO UPDATE SET
                    quantidade = EXCLUDED.quantidade,
                    preco_medio = EXCLUDED.preco_medio,
                    custo_total = EXCLUDED.custo_total
                """,
                ct);

            return Result.Success();
        }
        catch (PostgresException pg)
        {
            await dbContext.DesfazerTransacaoAmbienteAsync(ct);

            var erro = PostgresExceptionTranslator.Traduzir(pg);
            if (erro is null)
            {
                throw;
            }

            return Result.Failure(erro);
        }
    }

    public async Task<Result> RemoverAsync(string clienteId, string instrumentoId, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                DELETE FROM posicao_corrente
                WHERE cliente_id = {clienteId} AND instrumento_id = {instrumentoId}
                """,
                ct);

            return Result.Success();
        }
        catch (PostgresException pg)
        {
            await dbContext.DesfazerTransacaoAmbienteAsync(ct);

            var erro = PostgresExceptionTranslator.Traduzir(pg);
            if (erro is null)
            {
                throw;
            }

            return Result.Failure(erro);
        }
    }
}

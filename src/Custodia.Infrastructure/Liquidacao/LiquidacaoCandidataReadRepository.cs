using Custodia.Application.Liquidacao;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Liquidacao;

public sealed class LiquidacaoCandidataReadRepository(NpgsqlDataSource dataSource) : ILiquidacaoCandidataReadRepository
{
    static LiquidacaoCandidataReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    private const string SqlAbertasNaoRevertidas =
        """
        SELECT
            a.cliente_id AS "ClienteId",
            substring(a.ref_externa from 6) AS "TradeId",
            a.data_evento AS "DataEvento"
        FROM movimentos a
        WHERE a.tipo = 'a_liquidar'
          AND NOT EXISTS (
              SELECT 1 FROM movimentos adj WHERE adj.ref_estorno = a.id
          )
          AND NOT EXISTS (
              SELECT 1
              FROM movimentos principal
              JOIN movimentos adjp ON adjp.ref_estorno = principal.id
              WHERE principal.cliente_id = a.cliente_id
                AND principal.ref_externa = substring(a.ref_externa from 6)
          )
          AND NOT EXISTS (
              SELECT 1 FROM movimentos liq
              WHERE liq.cliente_id = a.cliente_id
                AND liq.ref_externa = 'liq:' || substring(a.ref_externa from 6) || ':brl'
          )
        ORDER BY a.data_evento, a.registrado_em, a.id
        """;

    public async Task<Result<IReadOnlyList<LiquidacaoCandidata>>> ObterAbertasNaoRevertidasAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<LiquidacaoCandidata>(
            new CommandDefinition(SqlAbertasNaoRevertidas, cancellationToken: ct));

        IReadOnlyList<LiquidacaoCandidata> candidatas = rows.ToList();

        return Result<IReadOnlyList<LiquidacaoCandidata>>.Success(candidatas);
    }
}

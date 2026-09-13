using Custodia.Application.Reparo;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Reparo;

public sealed class RepararResgatesAntigosReadRepository(NpgsqlDataSource dataSource) : IRepararResgatesAntigosReadRepository
{
    static RepararResgatesAntigosReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    internal const string SqlResgatesSemAliq =
        """
        SELECT
            v.cliente_id AS "ClienteId",
            v.instrumento_id AS "InstrumentoId",
            v.ref_externa AS "TradeId"
        FROM movimentos v
        WHERE v.tipo = 'venda'
          AND NOT EXISTS (SELECT 1 FROM movimentos adj WHERE adj.ref_estorno = v.id)
          AND NOT EXISTS (
              SELECT 1 FROM movimentos aliq
              WHERE aliq.cliente_id = v.cliente_id AND aliq.ref_externa = 'aliq:' || v.ref_externa
          )
        ORDER BY v.data_evento, v.registrado_em, v.id
        """;

    internal const string SqlAjustesDeResgateSemReversao =
        """
        SELECT
            adj.cliente_id AS "ClienteId",
            adj.ref_externa AS "EstornoTradeId",
            alvo.ref_externa AS "ResgateTradeId"
        FROM movimentos adj
        JOIN movimentos alvo ON alvo.id = adj.ref_estorno
        WHERE adj.tipo = 'ajuste'
          AND alvo.tipo = 'venda'
          AND EXISTS (
              SELECT 1 FROM movimentos aliq
              WHERE aliq.cliente_id = adj.cliente_id AND aliq.ref_externa = 'aliq:' || alvo.ref_externa
          )
          AND NOT EXISTS (
              SELECT 1 FROM movimentos estaliq
              WHERE estaliq.cliente_id = adj.cliente_id AND estaliq.ref_externa = 'est:aliq:' || adj.ref_externa
          )
        ORDER BY adj.data_evento, adj.registrado_em, adj.id
        """;

    public async Task<Result<IReadOnlyList<ResgateSemAliq>>> ObterResgatesSemAliqAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<ResgateSemAliq>(
            new CommandDefinition(SqlResgatesSemAliq, cancellationToken: ct));

        IReadOnlyList<ResgateSemAliq> candidatos = rows.ToList();

        return Result<IReadOnlyList<ResgateSemAliq>>.Success(candidatos);
    }

    public async Task<Result<IReadOnlyList<AjusteDeResgateSemReversao>>> ObterAjustesDeResgateSemReversaoAsync(
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<AjusteDeResgateSemReversao>(
            new CommandDefinition(SqlAjustesDeResgateSemReversao, cancellationToken: ct));

        IReadOnlyList<AjusteDeResgateSemReversao> candidatos = rows.ToList();

        return Result<IReadOnlyList<AjusteDeResgateSemReversao>>.Success(candidatos);
    }
}

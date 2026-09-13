using Custodia.Application.Conciliacao;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Conciliacao;

public sealed class ConciliacaoDeResgatesReadRepository(NpgsqlDataSource dataSource) : IConciliacaoDeResgatesReadRepository
{
    static ConciliacaoDeResgatesReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    internal const string SqlResgatesTributados =
        """
        SELECT
            v.cliente_id AS "ClienteId",
            v.instrumento_id AS "InstrumentoId",
            v.ref_externa AS "TradeId"
        FROM movimentos v
        WHERE v.tipo = 'venda'
          AND NOT EXISTS (SELECT 1 FROM movimentos adj WHERE adj.ref_estorno = v.id)
          AND EXISTS (
              SELECT 1 FROM movimentos aliq
              WHERE aliq.cliente_id = v.cliente_id AND aliq.ref_externa = 'aliq:' || v.ref_externa
          )
        ORDER BY v.data_evento, v.registrado_em, v.id
        """;

    public async Task<Result<IReadOnlyList<ResgateTributado>>> ObterResgatesTributadosAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<ResgateTributado>(
            new CommandDefinition(SqlResgatesTributados, cancellationToken: ct));

        IReadOnlyList<ResgateTributado> candidatos = rows.ToList();

        return Result<IReadOnlyList<ResgateTributado>>.Success(candidatos);
    }
}

using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class PosicaoCorrenteReadRepository(NpgsqlDataSource dataSource) : IPosicaoCorrenteReadRepository
{
    static PosicaoCorrenteReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    private const string SqlObter =
        """
        SELECT
            quantidade AS "Quantidade",
            preco_medio AS "PrecoMedio",
            custo_total AS "CustoTotal"
        FROM posicao_corrente
        WHERE cliente_id = @clienteId AND instrumento_id = @instrumentoId
        """;

    private sealed record PosicaoRow(decimal Quantidade, decimal? PrecoMedio, decimal? CustoTotal);

    public async Task<Result<PosicaoTresColunas>> ObterAsync(string clienteId, string instrumentoId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<PosicaoRow>(
            new CommandDefinition(SqlObter, new { clienteId, instrumentoId }, cancellationToken: ct));

        var estado = row is null
            ? PosicaoTresColunas.Zero
            : new PosicaoTresColunas(row.Quantidade, row.CustoTotal ?? 0m, row.PrecoMedio ?? 0m);

        return Result<PosicaoTresColunas>.Success(estado);
    }
}

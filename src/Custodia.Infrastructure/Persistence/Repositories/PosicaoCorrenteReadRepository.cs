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

    private const string SqlChaves =
        """
        SELECT cliente_id AS "ClienteId", instrumento_id AS "InstrumentoId"
        FROM posicao_corrente
        WHERE (@clienteId::text IS NULL OR cliente_id = @clienteId)
          AND (@instrumentoId::text IS NULL OR instrumento_id = @instrumentoId)
        ORDER BY "ClienteId", "InstrumentoId"
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

    public async Task<Result<IReadOnlyList<ChavePosicao>>> ObterChavesAsync(
        string? clienteId, string? instrumentoId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<ChavePosicao>(
            new CommandDefinition(SqlChaves, new { clienteId, instrumentoId }, cancellationToken: ct));

        IReadOnlyList<ChavePosicao> chaves = rows.ToList();

        return Result<IReadOnlyList<ChavePosicao>>.Success(chaves);
    }
}

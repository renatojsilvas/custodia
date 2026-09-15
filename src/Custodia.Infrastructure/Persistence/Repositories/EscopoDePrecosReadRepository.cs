using Custodia.Application.Precos.Bootstrap;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class EscopoDePrecosReadRepository(NpgsqlDataSource dataSource) : IEscopoDePrecosReadRepository
{
    static EscopoDePrecosReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    private static readonly string PadraoCaixa = $"{InstrumentosCaixa.Prefixo}%";

    private const string SqlLivroInteiro =
        """
        SELECT DISTINCT instrumento_id AS "InstrumentoId"
        FROM movimentos
        WHERE instrumento_id NOT ILIKE @padraoCaixa;

        SELECT MIN(data_evento)
        FROM movimentos
        WHERE instrumento_id NOT ILIKE @padraoCaixa;
        """;

    private const string SqlSemPrecoAtual =
        """
        SELECT DISTINCT m.instrumento_id AS "InstrumentoId"
        FROM movimentos m
        LEFT JOIN preco_atual p ON p.instrumento_id = m.instrumento_id
        WHERE m.instrumento_id NOT ILIKE @padraoCaixa
          AND p.instrumento_id IS NULL
        """;

    public async Task<Result<EscopoLivroInteiroConsulta>> ObterLivroInteiroAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(SqlLivroInteiro, new { padraoCaixa = PadraoCaixa }, cancellationToken: ct));

        var instrumentos = (await multi.ReadAsync<string>()).ToList();
        var menorDataEvento = await multi.ReadSingleOrDefaultAsync<DateOnly?>();

        return Result<EscopoLivroInteiroConsulta>.Success(
            new EscopoLivroInteiroConsulta(instrumentos, menorDataEvento));
    }

    public async Task<Result<IReadOnlyList<string>>> ObterSemPrecoAtualAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var instrumentos = await connection.QueryAsync<string>(
            new CommandDefinition(SqlSemPrecoAtual, new { padraoCaixa = PadraoCaixa }, cancellationToken: ct));

        return Result<IReadOnlyList<string>>.Success(instrumentos.ToList());
    }
}

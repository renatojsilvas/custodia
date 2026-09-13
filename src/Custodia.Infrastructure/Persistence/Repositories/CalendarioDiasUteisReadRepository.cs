using Custodia.Application.Calendario;
using Custodia.Domain.Common;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class CalendarioDiasUteisReadRepository(NpgsqlDataSource dataSource) : ICalendarioDiasUteisReadRepository
{
    static CalendarioDiasUteisReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    private const string SqlProximoDiaUtil =
        """
        SELECT MIN(data)
        FROM calendario_dias_uteis
        WHERE data > @data
        """;

    private const string SqlHorizonte =
        """
        SELECT
            MAX(data) AS "DataMaxima",
            (now() AT TIME ZONE 'America/Sao_Paulo')::date AS "Hoje"
        FROM calendario_dias_uteis
        """;

    public async Task<Result<ProximoDiaUtilConsulta>> ObterProximoDiaUtilAsync(DateOnly data, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var proximaData = await connection.ExecuteScalarAsync<DateOnly?>(
            new CommandDefinition(SqlProximoDiaUtil, new { data }, cancellationToken: ct));

        return Result<ProximoDiaUtilConsulta>.Success(
            proximaData is null ? ProximoDiaUtilConsulta.NaoEncontrado : ProximoDiaUtilConsulta.De(proximaData.Value));
    }

    public async Task<Result<HorizonteCalendarioConsulta>> ObterHorizonteAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var linha = await connection.QuerySingleAsync<HorizonteCalendarioRow>(
            new CommandDefinition(SqlHorizonte, cancellationToken: ct));

        return Result<HorizonteCalendarioConsulta>.Success(new HorizonteCalendarioConsulta(linha.DataMaxima, linha.Hoje));
    }

    private sealed record HorizonteCalendarioRow(DateOnly? DataMaxima, DateOnly Hoje);
}

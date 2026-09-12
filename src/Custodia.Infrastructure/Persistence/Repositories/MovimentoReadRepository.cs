using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class MovimentoReadRepository(NpgsqlDataSource dataSource) : IMovimentoReadRepository
{
    static MovimentoReadRepository()
    {
        DapperTypeHandlers.Register();
    }

    private const string ColunasMovimento =
        """
        id AS "Id",
        cliente_id AS "ClienteId",
        instrumento_id AS "InstrumentoId",
        tipo AS "Tipo",
        data_evento AS "DataEvento",
        registrado_em AS "RegistradoEm",
        qtd_delta AS "QtdDelta",
        valor_financeiro AS "ValorFinanceiro",
        ref_externa AS "RefExterna",
        ref_estorno AS "RefEstorno"
        """;

    private static readonly string SqlPorClienteERefExterna =
        $"""
        SELECT {ColunasMovimento}
        FROM movimentos
        WHERE cliente_id = @clienteId AND ref_externa = @refExterna
        """;

    private static readonly string SqlPorRefExternaEmQualquerCliente =
        $"""
        SELECT {ColunasMovimento}
        FROM movimentos
        WHERE ref_externa = @refExterna
        ORDER BY id ASC
        LIMIT 1
        """;

    private const string SqlMaxDataEvento =
        """
        SELECT MAX(data_evento)
        FROM movimentos
        WHERE cliente_id = @clienteId AND instrumento_id = @instrumentoId
        """;

    private static readonly string SqlMovimentosDaChave =
        $"""
        SELECT {ColunasMovimento}
        FROM movimentos
        WHERE cliente_id = @clienteId AND instrumento_id = @instrumentoId
        ORDER BY data_evento, registrado_em, id
        """;

    private const string SqlChavesDistintas =
        """
        SELECT DISTINCT cliente_id AS "ClienteId", instrumento_id AS "InstrumentoId"
        FROM movimentos
        WHERE (@clienteId::text IS NULL OR cliente_id = @clienteId)
          AND (@instrumentoId::text IS NULL OR instrumento_id = @instrumentoId)
        ORDER BY "ClienteId", "InstrumentoId"
        """;

    public async Task<Result<MovimentoConsulta>> ObterPorClienteERefExternaAsync(
        string clienteId, string refExterna, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<MovimentoRow>(
            new CommandDefinition(SqlPorClienteERefExterna, new { clienteId, refExterna }, cancellationToken: ct));

        return Result<MovimentoConsulta>.Success(
            row is null ? MovimentoConsulta.NaoEncontrado : MovimentoConsulta.DeLinha(MovimentoHidratador.Hidratar(row)));
    }

    public async Task<Result<MovimentoConsulta>> ObterPorRefExternaEmQualquerClienteAsync(
        string refExterna, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<MovimentoRow>(
            new CommandDefinition(SqlPorRefExternaEmQualquerCliente, new { refExterna }, cancellationToken: ct));

        return Result<MovimentoConsulta>.Success(
            row is null ? MovimentoConsulta.NaoEncontrado : MovimentoConsulta.DeLinha(MovimentoHidratador.Hidratar(row)));
    }

    public async Task<Result<MaxDataEventoConsulta>> ObterMaxDataEventoAsync(
        string clienteId, string instrumentoId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var maxDataEvento = await connection.ExecuteScalarAsync<DateOnly?>(
            new CommandDefinition(SqlMaxDataEvento, new { clienteId, instrumentoId }, cancellationToken: ct));

        return Result<MaxDataEventoConsulta>.Success(
            maxDataEvento is null ? MaxDataEventoConsulta.Inexistente : MaxDataEventoConsulta.De(maxDataEvento.Value));
    }

    public async Task<Result<IReadOnlyList<Movimento>>> ObterMovimentosDaChaveAsync(
        string clienteId, string instrumentoId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<MovimentoRow>(
            new CommandDefinition(SqlMovimentosDaChave, new { clienteId, instrumentoId }, cancellationToken: ct));

        IReadOnlyList<Movimento> movimentos = rows.Select(MovimentoHidratador.Hidratar).ToList();

        return Result<IReadOnlyList<Movimento>>.Success(movimentos);
    }

    public async Task<Result<IReadOnlyList<ChavePosicao>>> ObterChavesDistintasAsync(
        string? clienteId, string? instrumentoId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var rows = await connection.QueryAsync<ChavePosicao>(
            new CommandDefinition(SqlChavesDistintas, new { clienteId, instrumentoId }, cancellationToken: ct));

        IReadOnlyList<ChavePosicao> chaves = rows.ToList();

        return Result<IReadOnlyList<ChavePosicao>>.Success(chaves);
    }
}

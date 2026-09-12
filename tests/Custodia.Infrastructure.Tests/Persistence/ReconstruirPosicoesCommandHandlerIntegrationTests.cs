using Custodia.Application.Common.Interfaces;
using Custodia.Application.Posicoes;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class ReconstruirPosicoesCommandHandlerIntegrationTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateOnly Dia1 = new(2026, 8, 1);
    private static readonly DateOnly Dia2 = new(2026, 8, 2);
    private static readonly DateTimeOffset Agora = DateTimeOffset.UtcNow;

    private static string NovoClienteId() => $"cli-reconstruir-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:reconstruir-{Guid.NewGuid():N}";

    private static string NovaRefExterna() => $"ref-reconstruir-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => NpgsqlDataSource.Create(fixture.ConnectionString);

    private async Task InserirMovimentoAsync(
        string clienteId, string instrumentoId, string refExterna, TipoMovimento tipo,
        DateOnly dataEvento, decimal qtdDelta, decimal valorFinanceiro)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);
        var movimento = Movimento.Create(
            clienteId, instrumentoId, tipo, dataEvento, Agora, qtdDelta, valorFinanceiro, refExterna).Value;
        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);
    }

    private async Task CorromperPosicaoAsync(string clienteId, string instrumentoId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO posicao_corrente (cliente_id, instrumento_id, quantidade, preco_medio, custo_total)
            VALUES (@clienteId, @instrumentoId, 999, 999, 999)
            ON CONFLICT (cliente_id, instrumento_id) DO UPDATE SET
                quantidade = 999, preco_medio = 999, custo_total = 999
            """,
            new { clienteId, instrumentoId });
    }

    private ReconstruirPosicoesCommandHandler CriarHandler(AppDbContext dbContext) => new(
        new MovimentoReadRepository(CriarDataSource()),
        new PosicaoCorrenteReadRepository(CriarDataSource()),
        new PosicaoCorrenteWriteRepository(dbContext),
        dbContext);

    [Fact]
    public async Task Handle_ProjecaoCorrompidaNasTresColunas_DevolveAsTresColunasAoValorDoLivro()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await InserirMovimentoAsync(
            clienteId, instrumentoId, NovaRefExterna(), TipoMovimento.Compra, Dia1, 10m, 1000m);
        await InserirMovimentoAsync(
            clienteId, instrumentoId, NovaRefExterna(), TipoMovimento.Venda, Dia2, -4m, 600m);

        await CorromperPosicaoAsync(clienteId, instrumentoId);

        var readRepo = new PosicaoCorrenteReadRepository(CriarDataSource());
        var corrompida = await readRepo.ObterAsync(clienteId, instrumentoId, CancellationToken.None);
        Assert.Equal(new PosicaoTresColunas(999m, 999m, 999m), corrompida.Value);

        await using var db = fixture.CriarDbContext();
        var handler = CriarHandler(db);

        var resultado = await handler.Handle(
            new ReconstruirPosicoesCommand(clienteId, instrumentoId), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.ChavesReconstruidas);

        var reconstruida = await readRepo.ObterAsync(clienteId, instrumentoId, CancellationToken.None);
        Assert.Equal(new PosicaoTresColunas(6m, 600m, 100m), reconstruida.Value);
    }

    [Fact]
    public async Task Handle_ChaveOrfaNaProjecao_RemoveAChaveQueNaoExisteMaisNoLivro()
    {
        var clienteId = NovoClienteId();
        var instrumentoOrfao = NovoInstrumentoId();

        await CorromperPosicaoAsync(clienteId, instrumentoOrfao);

        await using var db = fixture.CriarDbContext();
        var handler = CriarHandler(db);

        var resultado = await handler.Handle(
            new ReconstruirPosicoesCommand(clienteId, instrumentoOrfao), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, resultado.Value.ChavesReconstruidas);
        Assert.Equal(1, resultado.Value.ChavesRemovidasPorOrfandade);

        var readRepo = new PosicaoCorrenteReadRepository(CriarDataSource());
        var apagada = await readRepo.ObterAsync(clienteId, instrumentoOrfao, CancellationToken.None);
        Assert.Equal(PosicaoTresColunas.Zero, apagada.Value);
    }
}

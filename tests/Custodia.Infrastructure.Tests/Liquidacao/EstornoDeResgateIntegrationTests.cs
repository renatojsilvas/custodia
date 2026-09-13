using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Eventos;
using Custodia.Application.Liquidacao;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Infrastructure.Common;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Custodia.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Liquidacao;

[Collection("infra-postgres")]
public sealed class EstornoDeResgateIntegrationTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateTimeOffset RegistradoEm = new(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);

    private static string NovoClienteId() => $"cli-estorno-resgate-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:estorno-resgate-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => fixture.DataSource;

    private ProcessarTradeRegisteredCommandHandler CriarHandlerDeEventos(AppDbContext db) =>
        new(
            new MovimentoReadRepository(CriarDataSource()),
            new MovimentoWriteRepository(db),
            new MovimentoTravamentoRepository(db),
            new PosicaoCorrenteReadRepository(CriarDataSource()),
            new PosicaoCorrenteWriteRepository(db),
            new AplicadorIncrementalDePosicao(new MovimentoReadRepository(CriarDataSource()), new PosicaoCorrenteReadRepository(CriarDataSource())),
            db,
            new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics(),
            new PausaEntreLerEGravarInerte());

    private LiquidarResgatesVencidosCommandHandler CriarHandlerDeLiquidacao(AppDbContext db) =>
        new(
            new AReceberVencidoReadRepository(CriarDataSource()),
            new MovimentoTravamentoRepository(db),
            new MovimentoReadRepository(CriarDataSource()),
            new MovimentoWriteRepository(db),
            new PosicaoCorrenteWriteRepository(db),
            new AplicadorIncrementalDePosicao(new MovimentoReadRepository(CriarDataSource()), new PosicaoCorrenteReadRepository(CriarDataSource())),
            db,
            new ProximoDiaUtilService(new CalendarioDiasUteisReadRepository(CriarDataSource())),
            new CalendarioDiasUteisReadRepository(CriarDataSource()),
            new FakeFilaDeRecalculo(),
            new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics(),
            new PausaEntreLerEGravarInerte(),
            new ConfigurationBuilder().Build(),
            TimeProvider.System);

    private async Task<Result<ResultadoTradeRegistered>> EnviarAsync(TradeRegisteredEvento evento)
    {
        await using var db = fixture.CriarDbContext();
        return await CriarHandlerDeEventos(db).Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);
    }

    private async Task<PosicaoTresColunas> ObterPosicaoAsync(string clienteId, string instrumentoId)
    {
        var repo = new PosicaoCorrenteReadRepository(CriarDataSource());
        var resultado = await repo.ObterAsync(clienteId, instrumentoId, CancellationToken.None);
        return resultado.Value;
    }

    private async Task AssertNenhumaLinhaDerivadaSemContrapartidaAsync(string clienteId, string tradeId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        foreach (var refExterna in new[]
                 {
                     $"ir:{tradeId}", $"iof:{tradeId}", $"aliq:{tradeId}", $"liq:{tradeId}:aliq", $"liq:{tradeId}:brl",
                 })
        {
            await using var buscaCommand = connection.CreateCommand();
            buscaCommand.CommandText = "SELECT id FROM movimentos WHERE cliente_id = @clienteId AND ref_externa = @refExterna";
            buscaCommand.Parameters.AddWithValue("clienteId", clienteId);
            buscaCommand.Parameters.AddWithValue("refExterna", refExterna);
            var id = await buscaCommand.ExecuteScalarAsync();

            if (id is null)
            {
                continue;
            }

            await using var contrapartidaCommand = connection.CreateCommand();
            contrapartidaCommand.CommandText = "SELECT COUNT(*) FROM movimentos WHERE ref_estorno = @id";
            contrapartidaCommand.Parameters.AddWithValue("id", (long)id);
            var contagem = (long)(await contrapartidaCommand.ExecuteScalarAsync())!;

            Assert.True(contagem > 0, $"linha derivada {refExterna} não tem contrapartida de reversão.");
        }
    }

    private static TradeRegisteredEvento CriarEventoDeCompra(string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valor) =>
        new(tradeId, clienteId, instrumentoId, OperacaoTrade.Aplicacao, quantidade, valor, dataEvento, RegistradoEm, null, "0");

    private static TradeRegisteredEvento CriarEventoDeResgate(string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valor) =>
        new(tradeId, clienteId, instrumentoId, OperacaoTrade.Resgate, quantidade, valor, dataEvento, RegistradoEm, null, null);

    private static TradeRegisteredEvento CriarEventoDeEstorno(string clienteId, string instrumentoId, string tradeIdDoEstorno, string tradeIdRevertido, DateOnly dataEvento, decimal quantidade, decimal valor) =>
        new(tradeIdDoEstorno, clienteId, instrumentoId, OperacaoTrade.Estorno, quantidade, valor, dataEvento, RegistradoEm, tradeIdRevertido, null);

    [Fact]
    public async Task Handle_EstornoDeResgateAntesDaLiquidacao_TrasTituloECaixaAZero()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var compraResultado = await EnviarAsync(CriarEventoDeCompra(clienteId, instrumentoId, "op-compra-antes", new DateOnly(2026, 1, 5), 10m, 1000m));
        Assert.True(compraResultado.IsSuccess);

        var tradeId = "op-resgate-antes";
        var resgateResultado = await EnviarAsync(CriarEventoDeResgate(clienteId, instrumentoId, tradeId, new DateOnly(2026, 8, 10), 10m, 1200m));
        Assert.True(resgateResultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resgateResultado.Value.Tipo);

        var estornoResultado = await EnviarAsync(
            CriarEventoDeEstorno(clienteId, instrumentoId, "op-estorno-antes", tradeId, new DateOnly(2026, 8, 12), 10m, 1200m));
        Assert.True(estornoResultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, estornoResultado.Value.Tipo);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), await ObterPosicaoAsync(clienteId, instrumentoId));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.Brl));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.ALiquidar));

        await AssertNenhumaLinhaDerivadaSemContrapartidaAsync(clienteId, tradeId);
    }

    [Fact]
    public async Task Handle_EstornoDeResgateDepoisDaLiquidacao_TrasTituloECaixaAZero()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var compraResultado = await EnviarAsync(CriarEventoDeCompra(clienteId, instrumentoId, "op-compra-depois", new DateOnly(2026, 1, 5), 10m, 1000m));
        Assert.True(compraResultado.IsSuccess);

        var tradeId = "op-resgate-depois";
        var resgateResultado = await EnviarAsync(CriarEventoDeResgate(clienteId, instrumentoId, tradeId, new DateOnly(2026, 8, 10), 10m, 1200m));
        Assert.True(resgateResultado.IsSuccess);

        await using (var dbJob = fixture.CriarDbContext())
        {
            var resultadoJob = await CriarHandlerDeLiquidacao(dbJob).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);
            Assert.True(resultadoJob.IsSuccess);
        }

        Assert.True(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:aliq"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:brl"));

        var estornoResultado = await EnviarAsync(
            CriarEventoDeEstorno(clienteId, instrumentoId, "op-estorno-depois", tradeId, new DateOnly(2026, 8, 12), 10m, 1200m));
        Assert.True(estornoResultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, estornoResultado.Value.Tipo);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), await ObterPosicaoAsync(clienteId, instrumentoId));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.Brl));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.ALiquidar));

        await AssertNenhumaLinhaDerivadaSemContrapartidaAsync(clienteId, tradeId);
    }

    [Fact]
    public async Task Handle_EstornoDeResgateSemIof_TrasTituloECaixaAZero()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var compraResultado = await EnviarAsync(CriarEventoDeCompra(clienteId, instrumentoId, "op-compra-sem-iof", new DateOnly(2026, 1, 5), 10m, 1000m));
        Assert.True(compraResultado.IsSuccess);

        var tradeId = "op-resgate-sem-iof";
        var resgateResultado = await EnviarAsync(CriarEventoDeResgate(clienteId, instrumentoId, tradeId, new DateOnly(2026, 3, 10), 10m, 1200m));
        Assert.True(resgateResultado.IsSuccess);

        Assert.False(await ExisteMovimentoAsync(clienteId, $"iof:{tradeId}"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"ir:{tradeId}"));

        var estornoResultado = await EnviarAsync(
            CriarEventoDeEstorno(clienteId, instrumentoId, "op-estorno-sem-iof", tradeId, new DateOnly(2026, 3, 12), 10m, 1200m));
        Assert.True(estornoResultado.IsSuccess);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), await ObterPosicaoAsync(clienteId, instrumentoId));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.Brl));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.ALiquidar));

        await AssertNenhumaLinhaDerivadaSemContrapartidaAsync(clienteId, tradeId);
    }

    [Fact]
    public async Task Handle_EstornoDeResgateComIofExistente_ReverteExplicitamenteOConjuntoEstIncluindoEstIof()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var compraResultado = await EnviarAsync(CriarEventoDeCompra(clienteId, instrumentoId, "op-compra-com-iof", new DateOnly(2026, 1, 1), 10m, 1000m));
        Assert.True(compraResultado.IsSuccess);

        var tradeId = "op-resgate-com-iof";
        var resgateResultado = await EnviarAsync(CriarEventoDeResgate(clienteId, instrumentoId, tradeId, new DateOnly(2026, 1, 10), 10m, 1200m));
        Assert.True(resgateResultado.IsSuccess);

        Assert.True(await ExisteMovimentoAsync(clienteId, $"ir:{tradeId}"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"iof:{tradeId}"));

        var estornoTradeId = "op-estorno-com-iof";
        var estornoResultado = await EnviarAsync(
            CriarEventoDeEstorno(clienteId, instrumentoId, estornoTradeId, tradeId, new DateOnly(2026, 1, 12), 10m, 1200m));
        Assert.True(estornoResultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, estornoResultado.Value.Tipo);

        Assert.Equal(new PosicaoTresColunas(10m, 1000m, 100m), await ObterPosicaoAsync(clienteId, instrumentoId));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.Brl));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.ALiquidar));

        Assert.True(await ExisteMovimentoAsync(clienteId, $"est:ir:{estornoTradeId}"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"est:iof:{estornoTradeId}"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"est:aliq:{estornoTradeId}"));

        await AssertNenhumaLinhaDerivadaSemContrapartidaAsync(clienteId, tradeId);
    }

    [Fact]
    public async Task Handle_EstornoDeResgateComPrejuizo_SemIrNemIof_TrasTituloECaixaAZero()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var compraResultado = await EnviarAsync(CriarEventoDeCompra(clienteId, instrumentoId, "op-compra-prejuizo", new DateOnly(2026, 1, 5), 10m, 3000m));
        Assert.True(compraResultado.IsSuccess);

        var tradeId = "op-resgate-prejuizo";
        var resgateResultado = await EnviarAsync(CriarEventoDeResgate(clienteId, instrumentoId, tradeId, new DateOnly(2026, 1, 10), 10m, 1000m));
        Assert.True(resgateResultado.IsSuccess);

        Assert.False(await ExisteMovimentoAsync(clienteId, $"ir:{tradeId}"));
        Assert.False(await ExisteMovimentoAsync(clienteId, $"iof:{tradeId}"));

        var estornoResultado = await EnviarAsync(
            CriarEventoDeEstorno(clienteId, instrumentoId, "op-estorno-prejuizo", tradeId, new DateOnly(2026, 1, 12), 10m, 1000m));
        Assert.True(estornoResultado.IsSuccess);

        Assert.Equal(new PosicaoTresColunas(10m, 3000m, 300m), await ObterPosicaoAsync(clienteId, instrumentoId));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.Brl));
        Assert.Equal(PosicaoTresColunas.Zero, await ObterPosicaoAsync(clienteId, InstrumentosCaixa.ALiquidar));

        await AssertNenhumaLinhaDerivadaSemContrapartidaAsync(clienteId, tradeId);
    }

    private async Task<bool> ExisteMovimentoAsync(string clienteId, string refExterna)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteId AND ref_externa = @refExterna";
        command.Parameters.AddWithValue("clienteId", clienteId);
        command.Parameters.AddWithValue("refExterna", refExterna);
        var contagem = (long)(await command.ExecuteScalarAsync())!;
        return contagem > 0;
    }
}

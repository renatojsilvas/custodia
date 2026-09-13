using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Eventos;
using Custodia.Application.Liquidacao;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Common;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Custodia.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Liquidacao;

[Collection("infra-postgres")]
public sealed class LiquidarResgatesVencidosCommandHandlerIntegrationTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateTimeOffset RegistradoEm = new(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);

    private static string NovoClienteId() => $"cli-job-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:job-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => fixture.DataSource;

    private static IConfiguration CriarConfiguracao(long? teto = null)
    {
        var dados = new Dictionary<string, string?>();
        if (teto is not null)
        {
            dados["Decisao:LiquidacaoTetoPorCiclo"] = teto.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
    }

    private LiquidarResgatesVencidosCommandHandler CriarHandlerDeLiquidacao(
        AppDbContext dbContext,
        IRecalculoEnfileiradorPort? recalculo = null,
        IPontoDeSuspensaoAposTravamento? pontoDeSuspensao = null,
        IBusinessMetrics? businessMetrics = null,
        long? teto = null) =>
        new(
            new LiquidacaoCandidataReadRepository(CriarDataSource()),
            new MovimentoTravamentoRepository(dbContext),
            new MovimentoReadRepository(CriarDataSource()),
            new MovimentoWriteRepository(dbContext),
            new PosicaoCorrenteWriteRepository(dbContext),
            new AplicadorIncrementalDePosicao(new MovimentoReadRepository(CriarDataSource()), new PosicaoCorrenteReadRepository(CriarDataSource())),
            dbContext,
            new ProximoDiaUtilService(new CalendarioDiasUteisReadRepository(CriarDataSource())),
            new CalendarioDiasUteisReadRepository(CriarDataSource()),
            recalculo ?? new FakeRecalculoEnfileiradorPort(),
            businessMetrics ?? new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics(),
            pontoDeSuspensao ?? new PontoDeSuspensaoAposTravamentoInerte(),
            CriarConfiguracao(teto),
            TimeProvider.System);

    private ProcessarTradeRegisteredCommandHandler CriarHandlerDeEventos(
        AppDbContext dbContext, IPontoDeSuspensaoAposTravamento? pontoDeSuspensao = null) =>
        new(
            new MovimentoReadRepository(CriarDataSource()),
            new MovimentoWriteRepository(dbContext),
            new MovimentoTravamentoRepository(dbContext),
            new PosicaoCorrenteReadRepository(CriarDataSource()),
            new PosicaoCorrenteWriteRepository(dbContext),
            new AplicadorIncrementalDePosicao(new MovimentoReadRepository(CriarDataSource()), new PosicaoCorrenteReadRepository(CriarDataSource())),
            dbContext,
            new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics(),
            pontoDeSuspensao ?? new PontoDeSuspensaoAposTravamentoInerte());

    private async Task<Movimento> InserirAsync(
        string clienteId, string instrumentoId, TipoMovimento tipo, DateOnly dataEvento, string refExterna,
        decimal qtdDelta, decimal valorFinanceiro, long? refEstorno = null)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);

        var movimento = Movimento.Create(
            clienteId, instrumentoId, tipo, dataEvento, RegistradoEm, qtdDelta, valorFinanceiro, refExterna, refEstorno).Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        return movimento;
    }

    private async Task<Movimento> InserirVendaAsync(
        string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valorBruto) =>
        await InserirAsync(clienteId, instrumentoId, TipoMovimento.Venda, dataEvento, tradeId, -quantidade, valorBruto);

    private async Task<Movimento> InserirAliqAsync(string clienteId, string tradeId, DateOnly dataEvento, decimal valorBruto) =>
        await InserirAsync(clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, dataEvento, $"aliq:{tradeId}", valorBruto, valorBruto);

    private async Task<(Movimento Venda, Movimento Aliq)> CriarFatoAsync(
        string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valorBruto)
    {
        var venda = await InserirVendaAsync(clienteId, instrumentoId, tradeId, dataEvento, quantidade, valorBruto);
        var aliq = await InserirAliqAsync(clienteId, tradeId, dataEvento, valorBruto);
        return (venda, aliq);
    }

    private async Task<long> ContarMovimentosAsync(string clienteId, string refExterna)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteId AND ref_externa = @refExterna";
        command.Parameters.AddWithValue("clienteId", clienteId);
        command.Parameters.AddWithValue("refExterna", refExterna);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> ExisteMovimentoAsync(string clienteId, string refExterna) =>
        await ContarMovimentosAsync(clienteId, refExterna) > 0;

    [Fact]
    public async Task Handle_JobRodadoDuasVezes_NaSegundaVezFazZeroInsert()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-idempotencia";
        var dataEvento = new DateOnly(2026, 8, 10);
        await CriarFatoAsync(clienteId, instrumentoId, tradeId, dataEvento, 10m, 1200m);

        await using (var db1 = fixture.CriarDbContext())
        {
            var resultado1 = await CriarHandlerDeLiquidacao(db1).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);
            Assert.True(resultado1.IsSuccess);
            Assert.True(resultado1.Value.FatosLiquidados >= 1);
        }

        Assert.True(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:aliq"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:brl"));

        var contagemAposPrimeiraExecucao = await ContarMovimentosAsync(clienteId, $"liq:{tradeId}:aliq");

        await using (var db2 = fixture.CriarDbContext())
        {
            var resultado2 = await CriarHandlerDeLiquidacao(db2).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);
            Assert.True(resultado2.IsSuccess);
        }

        var contagemAposSegundaExecucao = await ContarMovimentosAsync(clienteId, $"liq:{tradeId}:aliq");
        Assert.Equal(contagemAposPrimeiraExecucao, contagemAposSegundaExecucao);
    }

    [Theory]
    [InlineData("2026-08-07", "2026-08-10")]
    [InlineData("2026-08-08", "2026-08-10")]
    [InlineData("2026-04-20", "2026-04-22")]
    public async Task Handle_DataDeLiquidacao_PulaFimDeSemanaEFeriado_ELiquidaNoProximoDiaUtil(
        string dataEventoTexto, string dataLiquidacaoEsperadaTexto)
    {
        var dataEvento = DateOnly.Parse(dataEventoTexto);
        var dataLiquidacaoEsperada = DateOnly.Parse(dataLiquidacaoEsperadaTexto);

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = $"op-boundary-{dataEventoTexto}";
        await CriarFatoAsync(clienteId, instrumentoId, tradeId, dataEvento, 10m, 1000m);

        var recalculo = new FakeRecalculoEnfileiradorPort();
        await using var db = fixture.CriarDbContext();
        var resultado = await CriarHandlerDeLiquidacao(db, recalculo: recalculo).Handle(
            new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        var readRepo = new MovimentoReadRepository(CriarDataSource());
        var pernaAliqResult = await readRepo.ObterPorClienteERefExternaAsync(clienteId, $"liq:{tradeId}:aliq", CancellationToken.None);
        Assert.True(pernaAliqResult.Value.Encontrado);
        Assert.Equal(dataLiquidacaoEsperada, pernaAliqResult.Value.Linha!.DataEvento);

        var chamadasDesteFato = recalculo.Chamadas.Where(c => c.ClienteId == clienteId).ToList();
        Assert.Equal(2, chamadasDesteFato.Count);
        Assert.Contains(chamadasDesteFato, c => c.InstrumentoId == InstrumentosCaixa.ALiquidar && c.Desde == dataLiquidacaoEsperada);
        Assert.Contains(chamadasDesteFato, c => c.InstrumentoId == InstrumentosCaixa.Brl && c.Desde == dataLiquidacaoEsperada);
    }

    [Fact]
    public async Task Handle_ALiquidarComAjusteRevertendo_NaoLiquida_ComControlePositivoDeOutroFatoNaoRevertidoNoMesmoCiclo()
    {
        var clienteId = NovoClienteId();
        var dataEvento = new DateOnly(2026, 8, 10);

        var (_, aliqRevertido) = await CriarFatoAsync(clienteId, NovoInstrumentoId(), "op-job-revertido", dataEvento, 10m, 1200m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Ajuste, dataEvento.AddDays(1), "est:aliq:op-estorno-x",
            -1200m, -1200m, refEstorno: aliqRevertido.Id);

        await CriarFatoAsync(clienteId, NovoInstrumentoId(), "op-job-valido", dataEvento, 10m, 1200m);

        await using var db = fixture.CriarDbContext();
        var resultado = await CriarHandlerDeLiquidacao(db).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        Assert.False(await ExisteMovimentoAsync(clienteId, "liq:op-job-revertido:aliq"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "liq:op-job-valido:aliq"));
    }

    [Fact]
    public async Task Handle_CandidataOrfaSemMovimentoPrincipal_EPulada_AlertaComARefExternaOfensora_ELiquidaOsSadiosDoMesmoCiclo()
    {
        var clienteId = NovoClienteId();
        var dataEvento = new DateOnly(2026, 8, 10);

        await InserirAliqAsync(clienteId, "op-orfao-real", dataEvento, 1200m);
        await CriarFatoAsync(clienteId, NovoInstrumentoId(), "op-valido-real", dataEvento, 10m, 1200m);

        var metrics = new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics();

        await using var db = fixture.CriarDbContext();
        var resultado = await CriarHandlerDeLiquidacao(db, businessMetrics: metrics).Handle(
            new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(DesfechoLiquidacaoDeResgates.ParcialPorInconsistencia, resultado.Value.Desfecho);
        Assert.True(resultado.Value.FatosInconsistentes >= 1);

        Assert.False(await ExisteMovimentoAsync(clienteId, "liq:op-orfao-real:aliq"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "liq:op-valido-real:aliq"));

        Assert.Contains(
            metrics.CandidatasInconsistentes,
            a => a.ClienteId == clienteId && a.TradeId == "op-orfao-real" && a.RefExternaOfensora == "aliq:op-orfao-real");
    }

    [Fact]
    public async Task Handle_CandidatasAcimaDoTeto_DevolveFalha_NenhumaLinhaEhGravada()
    {
        var clienteId = NovoClienteId();
        var dataEvento = new DateOnly(2026, 8, 10);

        await CriarFatoAsync(clienteId, NovoInstrumentoId(), "op-teto-1", dataEvento, 10m, 1000m);
        await CriarFatoAsync(clienteId, NovoInstrumentoId(), "op-teto-2", dataEvento, 10m, 1000m);

        await using var db = fixture.CriarDbContext();
        var resultado = await CriarHandlerDeLiquidacao(db, teto: 1).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(LiquidacaoErrors.LimiteDeCandidatasPorCicloExcedido, resultado.Error);

        Assert.False(await ExisteMovimentoAsync(clienteId, "liq:op-teto-1:aliq"));
        Assert.False(await ExisteMovimentoAsync(clienteId, "liq:op-teto-2:aliq"));
    }

    private static TradeRegisteredEvento CriarEventoDeEstorno(string tradeIdDoEstorno, Movimento titulo, DateOnly dataEvento) =>
        new(
            tradeIdDoEstorno,
            titulo.ClienteId,
            titulo.InstrumentoId,
            OperacaoTrade.Estorno,
            Math.Abs(titulo.QtdDelta),
            titulo.ValorFinanceiro,
            dataEvento,
            RegistradoEm,
            titulo.RefExterna,
            null);

    [Fact]
    public async Task Handle_RaceEstornoAdquireOTravamentoPrimeiro_JobNaoLiquidaOFatoRevertido()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-race-estorno-primeiro";
        var dataEvento = new DateOnly(2026, 8, 10);
        var (venda, _) = await CriarFatoAsync(clienteId, instrumentoId, tradeId, dataEvento, 10m, 1200m);

        var estornoTravado = new TaskCompletionSource();
        var podeContinuarEstorno = new TaskCompletionSource();

        await using var dbEstorno = fixture.CriarDbContext();
        var pontoDeSuspensaoDoEstorno = new FuncPontoDeSuspensaoAposTravamento(async (_, refExterna, ct) =>
        {
            if (refExterna != $"aliq:{tradeId}")
            {
                return;
            }

            estornoTravado.TrySetResult();
            await podeContinuarEstorno.Task.WaitAsync(ct);
        });

        var estornoHandler = CriarHandlerDeEventos(dbEstorno, pontoDeSuspensaoDoEstorno);
        var eventoDeEstorno = CriarEventoDeEstorno("op-estorno-race-1", venda, dataEvento.AddDays(1));

        var estornoTask = estornoHandler.Handle(new ProcessarTradeRegisteredCommand(eventoDeEstorno), CancellationToken.None);

        await estornoTravado.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using var dbJob = fixture.CriarDbContext();
        var jobTask = CriarHandlerDeLiquidacao(dbJob).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        var jobTerminouCedoDemais = await Task.WhenAny(jobTask, Task.Delay(TimeSpan.FromMilliseconds(500))) == jobTask;
        Assert.False(jobTerminouCedoDemais, "o job não deveria conseguir travar a linha aliq: enquanto o estorno a mantém travada.");

        podeContinuarEstorno.SetResult();

        var resultadoEstorno = await estornoTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(resultadoEstorno.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultadoEstorno.Value.Tipo);

        var resultadoJob = await jobTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(resultadoJob.IsSuccess);

        Assert.False(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:aliq"));
        Assert.False(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:brl"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"est:aliq:op-estorno-race-1"));
    }

    [Fact]
    public async Task Handle_RaceJobAdquireOTravamentoPrimeiro_EstornoReverteOConjuntoDeLiquidacaoRecemGravado()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-race-job-primeiro";
        var dataEvento = new DateOnly(2026, 8, 10);
        var (venda, _) = await CriarFatoAsync(clienteId, instrumentoId, tradeId, dataEvento, 10m, 1200m);

        var jobTravado = new TaskCompletionSource();
        var podeContinuarJob = new TaskCompletionSource();

        await using var dbJob = fixture.CriarDbContext();
        var pontoDeSuspensaoDoJob = new FuncPontoDeSuspensaoAposTravamento(async (_, refExterna, ct) =>
        {
            if (refExterna != $"aliq:{tradeId}")
            {
                return;
            }

            jobTravado.TrySetResult();
            await podeContinuarJob.Task.WaitAsync(ct);
        });

        var jobTask = CriarHandlerDeLiquidacao(dbJob, pontoDeSuspensao: pontoDeSuspensaoDoJob)
            .Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        await jobTravado.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using var dbEstorno = fixture.CriarDbContext();
        var eventoDeEstorno = CriarEventoDeEstorno("op-estorno-race-2", venda, dataEvento.AddDays(1));
        var estornoTask = CriarHandlerDeEventos(dbEstorno)
            .Handle(new ProcessarTradeRegisteredCommand(eventoDeEstorno), CancellationToken.None);

        var estornoTerminouCedoDemais = await Task.WhenAny(estornoTask, Task.Delay(TimeSpan.FromMilliseconds(500))) == estornoTask;
        Assert.False(estornoTerminouCedoDemais, "o estorno não deveria conseguir travar a linha aliq: enquanto o job a mantém travada.");

        podeContinuarJob.SetResult();

        var resultadoJob = await jobTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(resultadoJob.IsSuccess);

        var resultadoEstorno = await estornoTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(resultadoEstorno.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultadoEstorno.Value.Tipo);

        Assert.True(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:aliq"));
        Assert.True(await ExisteMovimentoAsync(clienteId, $"liq:{tradeId}:brl"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "est:liq:op-estorno-race-2:aliq"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "est:liq:op-estorno-race-2:brl"));
    }
}

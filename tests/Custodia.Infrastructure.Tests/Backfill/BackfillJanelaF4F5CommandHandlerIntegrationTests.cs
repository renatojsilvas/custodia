using Custodia.Application.Backfill;
using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Liquidacao;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Infrastructure.Backfill;
using Custodia.Infrastructure.Common;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Tests.Liquidacao;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Custodia.Infrastructure.Tests.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Backfill;

[Collection("infra-postgres")]
public sealed class BackfillJanelaF4F5CommandHandlerIntegrationTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateTimeOffset RegistradoEm = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

    private static string NovoClienteId() => $"cli-backfill-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:backfill-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => fixture.DataSource;

    private BackfillJanelaF4F5CommandHandler CriarHandlerDeBackfill(
        AppDbContext dbContext, IBusinessMetrics? metrics = null) =>
        new(
            new BackfillJanelaF4F5ReadRepository(CriarDataSource()),
            new MovimentoReadRepository(CriarDataSource()),
            new MovimentoWriteRepository(dbContext),
            new MovimentoTravamentoRepository(dbContext),
            new PosicaoCorrenteWriteRepository(dbContext),
            new AplicadorIncrementalDePosicao(new MovimentoReadRepository(CriarDataSource()), new PosicaoCorrenteReadRepository(CriarDataSource())),
            dbContext,
            metrics ?? new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics(),
            TimeProvider.System);

    private LiquidarResgatesVencidosCommandHandler CriarHandlerDeLiquidacao(AppDbContext dbContext) =>
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
            new FakeRecalculoEnfileiradorPort(),
            new Custodia.Infrastructure.Tests.Calendario.FakeBusinessMetrics(),
            new PontoDeSuspensaoAposTravamentoInerte(),
            new ConfigurationBuilder().Build(),
            TimeProvider.System);

    private async Task<Result<BackfillJanelaF4F5Resultado>> RodarBackfillAsync(IBusinessMetrics? metrics = null)
    {
        await using var db = fixture.CriarDbContext();
        return await CriarHandlerDeBackfill(db, metrics).Handle(new BackfillJanelaF4F5Command(), CancellationToken.None);
    }

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

    private Task<Movimento> InserirCompraAsync(
        string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valorFinanceiro) =>
        InserirAsync(clienteId, instrumentoId, TipoMovimento.Compra, dataEvento, tradeId, quantidade, valorFinanceiro);

    private Task<Movimento> InserirVendaSemDerivadosAsync(
        string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valorFinanceiro) =>
        InserirAsync(clienteId, instrumentoId, TipoMovimento.Venda, dataEvento, tradeId, -quantidade, valorFinanceiro);

    private Task<Movimento> InserirAjusteSobreAsync(Movimento alvo, string clienteId, string refExterna) =>
        InserirAsync(
            clienteId, alvo.InstrumentoId, TipoMovimento.Ajuste, alvo.DataEvento, refExterna,
            -alvo.QtdDelta, -alvo.ValorFinanceiro, alvo.Id);

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

    private async Task<Movimento> ObterMovimentoAsync(string clienteId, string refExterna)
    {
        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterPorClienteERefExternaAsync(clienteId, refExterna, CancellationToken.None);
        Assert.True(resultado.Value.Encontrado, $"movimento {refExterna} não encontrado.");
        return resultado.Value.Linha!;
    }

    private async Task<PosicaoTresColunas> ObterPosicaoAsync(string clienteId, string instrumentoId)
    {
        var repo = new PosicaoCorrenteReadRepository(CriarDataSource());
        var resultado = await repo.ObterAsync(clienteId, instrumentoId, CancellationToken.None);
        return resultado.Value;
    }

    [Fact]
    public async Task Handle_ResgateSemDerivados_ComBasePositivaEPrazoMenorQue30Dias_RecebeIrIofEAliqEAtualizaPosicao()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await InserirCompraAsync(clienteId, instrumentoId, "op-compra-1", new DateOnly(2026, 6, 1), 10m, 1000m);
        await InserirVendaSemDerivadosAsync(clienteId, instrumentoId, "op-resgate-1", new DateOnly(2026, 6, 11), 10m, 1200m);

        var resultado = await RodarBackfillAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.ResgatesCandidatos);
        Assert.Equal(1, resultado.Value.ResgatesBackfilled);

        var ir = await ObterMovimentoAsync(clienteId, "ir:op-resgate-1");
        var iof = await ObterMovimentoAsync(clienteId, "iof:op-resgate-1");
        var aliq = await ObterMovimentoAsync(clienteId, "aliq:op-resgate-1");

        Assert.Equal(15.30m, ir.ValorFinanceiro);
        Assert.Equal(132.00m, iof.ValorFinanceiro);
        Assert.Equal(1200.00m, aliq.ValorFinanceiro);
        Assert.Equal(new DateOnly(2026, 6, 11), ir.DataEvento);
        Assert.Equal(new DateOnly(2026, 6, 11), iof.DataEvento);
        Assert.Equal(new DateOnly(2026, 6, 11), aliq.DataEvento);

        var posicaoALiquidar = await ObterPosicaoAsync(clienteId, InstrumentosCaixa.ALiquidar);
        Assert.Equal(1052.70m, posicaoALiquidar.Quantidade);
        Assert.Equal(1052.70m, posicaoALiquidar.CustoTotal);
        Assert.Equal(1.000000m, posicaoALiquidar.PrecoMedio);
    }

    [Fact]
    public async Task Handle_ResgateComPrejuizoEmTodosOsLotes_NaoRecebeIrNemIof_MasRecebeAliq()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await InserirCompraAsync(clienteId, instrumentoId, "op-compra-prejuizo", new DateOnly(2026, 1, 5), 10m, 3000m);
        await InserirVendaSemDerivadosAsync(clienteId, instrumentoId, "op-resgate-prejuizo", new DateOnly(2026, 1, 10), 10m, 1000m);

        var resultado = await RodarBackfillAsync();

        Assert.True(resultado.IsSuccess);
        Assert.False(await ExisteMovimentoAsync(clienteId, "ir:op-resgate-prejuizo"));
        Assert.False(await ExisteMovimentoAsync(clienteId, "iof:op-resgate-prejuizo"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "aliq:op-resgate-prejuizo"));
    }

    [Fact]
    public async Task Handle_ResgateComPrincipalRevertido_NaoRecebeDerivados_ComControlePositivoDeResgateNaoRevertido()
    {
        var clienteId = NovoClienteId();

        var instrumentoRevertido = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoRevertido, "op-compra-revertido", new DateOnly(2026, 6, 1), 10m, 1000m);
        var vendaRevertida = await InserirVendaSemDerivadosAsync(
            clienteId, instrumentoRevertido, "op-resgate-revertido", new DateOnly(2026, 6, 11), 10m, 1200m);
        await InserirAjusteSobreAsync(vendaRevertida, clienteId, "op-estorno-antigo-do-revertido");

        var instrumentoSaudavel = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoSaudavel, "op-compra-saudavel", new DateOnly(2026, 6, 1), 10m, 1000m);
        await InserirVendaSemDerivadosAsync(
            clienteId, instrumentoSaudavel, "op-resgate-saudavel", new DateOnly(2026, 6, 11), 10m, 1200m);

        var resultado = await RodarBackfillAsync();

        Assert.True(resultado.IsSuccess);

        Assert.False(await ExisteMovimentoAsync(clienteId, "ir:op-resgate-revertido"));
        Assert.False(await ExisteMovimentoAsync(clienteId, "iof:op-resgate-revertido"));
        Assert.False(await ExisteMovimentoAsync(clienteId, "aliq:op-resgate-revertido"));

        Assert.True(await ExisteMovimentoAsync(clienteId, "ir:op-resgate-saudavel"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "iof:op-resgate-saudavel"));
        Assert.True(await ExisteMovimentoAsync(clienteId, "aliq:op-resgate-saudavel"));
    }

    [Fact]
    public async Task Handle_OrdemMetadeIDepoisMetadeII_ProvadaPeloResultado_EstFamiliaApontaParaLinhaGravadaPelaMetadeI()
    {
        var clienteId = NovoClienteId();

        var instrumentoDoFatoRevertidoDepois = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoDoFatoRevertidoDepois, "op-compra-b", new DateOnly(2026, 6, 1), 10m, 1000m);
        await InserirVendaSemDerivadosAsync(
            clienteId, instrumentoDoFatoRevertidoDepois, "op-resgate-b", new DateOnly(2026, 6, 11), 10m, 1200m);

        var primeiraPassagem = await RodarBackfillAsync();
        Assert.True(primeiraPassagem.IsSuccess);
        Assert.Equal(1, primeiraPassagem.Value.ResgatesBackfilled);
        Assert.Equal(0, primeiraPassagem.Value.AjustesBackfilled);

        var irB = await ObterMovimentoAsync(clienteId, "ir:op-resgate-b");
        var iofB = await ObterMovimentoAsync(clienteId, "iof:op-resgate-b");
        var aliqB = await ObterMovimentoAsync(clienteId, "aliq:op-resgate-b");

        var vendaB = await ObterMovimentoAsync(clienteId, "op-resgate-b");
        await InserirAjusteSobreAsync(vendaB, clienteId, "op-estorno-tardio-do-b");

        var instrumentoDoFatoSaudavel = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoDoFatoSaudavel, "op-compra-a", new DateOnly(2026, 6, 1), 10m, 1000m);
        await InserirVendaSemDerivadosAsync(
            clienteId, instrumentoDoFatoSaudavel, "op-resgate-a", new DateOnly(2026, 6, 11), 10m, 1200m);

        var segundaPassagem = await RodarBackfillAsync();
        Assert.True(segundaPassagem.IsSuccess);
        Assert.Equal(1, segundaPassagem.Value.ResgatesBackfilled);
        Assert.Equal(1, segundaPassagem.Value.AjustesBackfilled);

        Assert.True(await ExisteMovimentoAsync(clienteId, "aliq:op-resgate-a"));

        var estIr = await ObterMovimentoAsync(clienteId, "est:ir:op-estorno-tardio-do-b");
        var estIof = await ObterMovimentoAsync(clienteId, "est:iof:op-estorno-tardio-do-b");
        var estAliq = await ObterMovimentoAsync(clienteId, "est:aliq:op-estorno-tardio-do-b");

        Assert.Equal(irB.Id, estIr.RefEstorno);
        Assert.Equal(iofB.Id, estIof.RefEstorno);
        Assert.Equal(aliqB.Id, estAliq.RefEstorno);

        var pendenciasResult = await new BackfillJanelaF4F5ReadRepository(CriarDataSource())
            .ObterAjustesDeResgateSemReversaoAsync(CancellationToken.None);
        Assert.True(pendenciasResult.IsSuccess);
        Assert.DoesNotContain(pendenciasResult.Value, p => p.EstornoTradeId == "op-estorno-tardio-do-b");
    }

    [Fact]
    public async Task Handle_RodadoDuasVezes_ResultadoIdenticoNasDuasMetades_EQuatroConsultasDeGuardaZeradas()
    {
        var clienteId = NovoClienteId();

        var instrumentoSaudavel = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoSaudavel, "op-compra-idem-1", new DateOnly(2026, 6, 1), 10m, 1000m);
        await InserirVendaSemDerivadosAsync(clienteId, instrumentoSaudavel, "op-resgate-idem-1", new DateOnly(2026, 6, 11), 10m, 1200m);

        var instrumentoPrejuizo = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoPrejuizo, "op-compra-idem-2", new DateOnly(2026, 1, 5), 10m, 3000m);
        await InserirVendaSemDerivadosAsync(clienteId, instrumentoPrejuizo, "op-resgate-idem-2", new DateOnly(2026, 1, 10), 10m, 1000m);

        var instrumentoRevertido = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoRevertido, "op-compra-idem-3", new DateOnly(2026, 6, 1), 10m, 1000m);
        var vendaRevertida = await InserirVendaSemDerivadosAsync(
            clienteId, instrumentoRevertido, "op-resgate-idem-3", new DateOnly(2026, 6, 11), 10m, 1200m);
        await InserirAjusteSobreAsync(vendaRevertida, clienteId, "op-estorno-idem-3");

        var primeira = await RodarBackfillAsync();
        Assert.True(primeira.IsSuccess);
        Assert.Equal(2, primeira.Value.ResgatesBackfilled);

        var segunda = await RodarBackfillAsync();
        Assert.True(segunda.IsSuccess);
        Assert.Equal(0, segunda.Value.ResgatesCandidatos);
        Assert.Equal(0, segunda.Value.ResgatesBackfilled);
        Assert.Equal(0, segunda.Value.AjustesCandidatos);
        Assert.Equal(0, segunda.Value.AjustesBackfilled);

        var guardaResgates = await new BackfillJanelaF4F5ReadRepository(CriarDataSource())
            .ObterResgatesSemAliqAsync(CancellationToken.None);
        Assert.True(guardaResgates.IsSuccess);
        Assert.DoesNotContain(guardaResgates.Value, r => r.ClienteId == clienteId);

        var guardaAjustes = await new BackfillJanelaF4F5ReadRepository(CriarDataSource())
            .ObterAjustesDeResgateSemReversaoAsync(CancellationToken.None);
        Assert.True(guardaAjustes.IsSuccess);
        Assert.DoesNotContain(guardaAjustes.Value, a => a.ClienteId == clienteId);
    }

    [Fact]
    public async Task Handle_JobReligadoDepoisDoBackfill_NaoCreditaCaixaBrlDeFatoComPrincipalRevertido_SeAliqNascesse()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await InserirCompraAsync(clienteId, instrumentoId, "op-compra-h3", new DateOnly(2026, 6, 1), 10m, 1000m);
        var vendaRevertida = await InserirVendaSemDerivadosAsync(
            clienteId, instrumentoId, "op-resgate-h3", new DateOnly(2026, 6, 11), 10m, 1200m);
        await InserirAjusteSobreAsync(vendaRevertida, clienteId, "op-estorno-h3");

        var resultadoBackfill = await RodarBackfillAsync();
        Assert.True(resultadoBackfill.IsSuccess);
        Assert.False(await ExisteMovimentoAsync(clienteId, "aliq:op-resgate-h3"));

        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 6, 11), "aliq:op-resgate-h3",
            1200m, 1200m);

        await using var db = fixture.CriarDbContext();
        var resultadoJob = await CriarHandlerDeLiquidacao(db).Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultadoJob.IsSuccess);
        Assert.False(await ExisteMovimentoAsync(clienteId, "liq:op-resgate-h3:aliq"));
        Assert.False(await ExisteMovimentoAsync(clienteId, "liq:op-resgate-h3:brl"));
    }
}

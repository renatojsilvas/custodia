using Custodia.Application.Calendario;
using Custodia.Application.Conciliacao;
using Custodia.Application.Liquidacao;
using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Reparo;
using Custodia.Infrastructure.Calendario;
using Custodia.Infrastructure.Conciliacao;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Persistence.Repositories;
using Custodia.Infrastructure.Tests.Persistence;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Conciliacao;

[Collection("infra-postgres")]
public sealed class ExecutarConciliacaoDeResgatesCommandHandlerIntegrationTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateTimeOffset RegistradoEm = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

    private static string NovoClienteId() => $"cli-guardas-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:guardas-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => fixture.DataSource;

    private ExecutarConciliacaoDeResgatesCommandHandler CriarHandlerDeConciliacao() =>
        new(
            new RepararResgatesAntigosReadRepository(CriarDataSource()),
            new AReceberVencidoReadRepository(CriarDataSource()),
            new ConciliacaoDeResgatesReadRepository(CriarDataSource()),
            new MovimentoReadRepository(CriarDataSource()),
            new ProximoDiaUtilService(new CalendarioDiasUteisReadRepository(CriarDataSource())),
            new CalendarioDiasUteisReadRepository(CriarDataSource()));

    private async Task<ResultadoConciliacaoDeResgates> ExecutarConciliacaoAsync()
    {
        var resultado = await CriarHandlerDeConciliacao().Handle(new ExecutarConciliacaoDeResgatesCommand(), CancellationToken.None);
        Assert.True(resultado.IsSuccess);
        return resultado.Value;
    }

    private async Task<Movimento> InserirAsync(
        string clienteId, string instrumentoId, TipoMovimento tipo, DateOnly dataEvento, string refExterna,
        decimal qtdDelta, decimal valorFinanceiro, long? refEstorno = null, DateTimeOffset? registradoEm = null)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);

        var movimento = Movimento.Create(
            clienteId, instrumentoId, tipo, dataEvento, registradoEm ?? RegistradoEm, qtdDelta, valorFinanceiro, refExterna, refEstorno).Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        return movimento;
    }

    private Task<Movimento> InserirCompraAsync(
        string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valorFinanceiro,
        DateTimeOffset? registradoEm = null) =>
        InserirAsync(clienteId, instrumentoId, TipoMovimento.Compra, dataEvento, tradeId, quantidade, valorFinanceiro, registradoEm: registradoEm);

    private Task<Movimento> InserirVendaAsync(
        string clienteId, string instrumentoId, string tradeId, DateOnly dataEvento, decimal quantidade, decimal valorFinanceiro) =>
        InserirAsync(clienteId, instrumentoId, TipoMovimento.Venda, dataEvento, tradeId, -quantidade, valorFinanceiro);

    private Task<Movimento> InserirAjusteSobreAsync(Movimento alvo, string clienteId, string refExterna) =>
        InserirAsync(
            clienteId, alvo.InstrumentoId, TipoMovimento.Ajuste, alvo.DataEvento, refExterna,
            -alvo.QtdDelta, -alvo.ValorFinanceiro, alvo.Id);

    [Fact]
    public async Task Guarda1_ResgateEfetivoSemAliq_Acusa_ComControleNegativoDePrejuizoQueTemAliqSemIrNemIof()
    {
        var antes = await ExecutarConciliacaoAsync();

        var clienteId = NovoClienteId();
        var instrumentoAcusa = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoAcusa, "g1-compra-acusa", new DateOnly(2026, 6, 1), 10m, 1000m);
        await InserirVendaAsync(clienteId, instrumentoAcusa, "g1-resgate-acusa", new DateOnly(2026, 6, 11), 10m, 1200m);

        var depoisDoPositivo = await ExecutarConciliacaoAsync();
        Assert.Equal(antes.ResgatesSemAliq + 1, depoisDoPositivo.ResgatesSemAliq);

        var instrumentoPrejuizo = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoPrejuizo, "g1-compra-prejuizo", new DateOnly(2026, 1, 5), 10m, 3000m);
        await InserirVendaAsync(clienteId, instrumentoPrejuizo, "g1-resgate-prejuizo", new DateOnly(2026, 1, 10), 10m, 1000m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 1, 10), "aliq:g1-resgate-prejuizo",
            1000m, 1000m);

        var depoisDoControleNegativo = await ExecutarConciliacaoAsync();
        Assert.Equal(depoisDoPositivo.ResgatesSemAliq, depoisDoControleNegativo.ResgatesSemAliq);
    }

    [Fact]
    public async Task Guarda2_AjusteSobreResgateSemConjuntoEst_Acusa_ComControleNegativoDeReversaoCompletaSemIof()
    {
        var antes = await ExecutarConciliacaoAsync();

        var clienteId = NovoClienteId();

        var instrumentoAcusa = NovoInstrumentoId();
        var vendaAcusa = await InserirVendaAsync(clienteId, instrumentoAcusa, "g2-resgate-acusa", new DateOnly(2026, 6, 11), 10m, 1200m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 6, 11), "aliq:g2-resgate-acusa",
            1200m, 1200m);
        await InserirAjusteSobreAsync(vendaAcusa, clienteId, "g2-estorno-acusa");

        var depoisDoPositivo = await ExecutarConciliacaoAsync();
        Assert.Equal(antes.AjustesDeResgateSemReversao + 1, depoisDoPositivo.AjustesDeResgateSemReversao);

        var instrumentoCompleto = NovoInstrumentoId();
        var vendaCompleta = await InserirVendaAsync(
            clienteId, instrumentoCompleto, "g2-resgate-completo", new DateOnly(2026, 3, 10), 10m, 1200m);
        var aliqCompleta = await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 3, 10), "aliq:g2-resgate-completo",
            1200m, 1200m);
        var irCompleta = await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido, new DateOnly(2026, 3, 10), "ir:g2-resgate-completo",
            -50m, 50m);
        var ajusteCompleto = await InserirAjusteSobreAsync(vendaCompleta, clienteId, "g2-estorno-completo");
        await InserirAjusteSobreAsync(aliqCompleta, clienteId, "est:aliq:g2-estorno-completo");
        await InserirAjusteSobreAsync(irCompleta, clienteId, "est:ir:g2-estorno-completo");
        _ = ajusteCompleto;

        var depoisDoControleNegativo = await ExecutarConciliacaoAsync();
        Assert.Equal(depoisDoPositivo.AjustesDeResgateSemReversao, depoisDoControleNegativo.AjustesDeResgateSemReversao);
    }

    [Fact]
    public async Task Guarda3_ALiquidarVencidaSemLiquidacao_Acusa_ComControleNegativoDeLiquidacaoJaGravada()
    {
        var antes = await ExecutarConciliacaoAsync();

        var clienteId = NovoClienteId();

        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 1, 5), "aliq:g3-vencida",
            1000m, 1000m);

        var depoisDoPositivo = await ExecutarConciliacaoAsync();
        Assert.Equal(antes.ALiquidarVencidaSemLiquidacao + 1, depoisDoPositivo.ALiquidarVencidaSemLiquidacao);

        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 1, 5), "aliq:g3-liquidada",
            1000m, 1000m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao, new DateOnly(2026, 1, 6), "liq:g3-liquidada:aliq",
            -1000m, 1000m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao, new DateOnly(2026, 1, 6), "liq:g3-liquidada:brl",
            1000m, 1000m);

        var depoisDoControleNegativo = await ExecutarConciliacaoAsync();
        Assert.Equal(depoisDoPositivo.ALiquidarVencidaSemLiquidacao, depoisDoControleNegativo.ALiquidarVencidaSemLiquidacao);
    }

    [Fact]
    public async Task Guarda4_TributoDivergenteDoRederivado_AcusaCompraRetroativa_ComControleNegativoDeTributoQueBate()
    {
        var antes = await ExecutarConciliacaoAsync();

        var clienteId = NovoClienteId();

        var instrumentoBate = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoBate, "g4-compra-bate", new DateOnly(2026, 1, 1), 10m, 1000m);
        await InserirVendaAsync(clienteId, instrumentoBate, "g4-resgate-bate", new DateOnly(2026, 1, 20), 10m, 1300m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido, new DateOnly(2026, 1, 20), "ir:g4-resgate-bate",
            -43.20m, 43.20m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Iof, new DateOnly(2026, 1, 20), "iof:g4-resgate-bate",
            -108.00m, 108.00m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 1, 20), "aliq:g4-resgate-bate",
            1300m, 1300m);

        var depoisDoControleNegativo = await ExecutarConciliacaoAsync();
        Assert.Equal(antes.TributosDivergentesDoRederivado, depoisDoControleNegativo.TributosDivergentesDoRederivado);

        var instrumentoDivergente = NovoInstrumentoId();
        await InserirCompraAsync(clienteId, instrumentoDivergente, "g4-compra-divergente", new DateOnly(2026, 1, 1), 10m, 1000m);
        await InserirVendaAsync(clienteId, instrumentoDivergente, "g4-resgate-divergente", new DateOnly(2026, 1, 20), 10m, 1300m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido, new DateOnly(2026, 1, 20), "ir:g4-resgate-divergente",
            -43.20m, 43.20m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Iof, new DateOnly(2026, 1, 20), "iof:g4-resgate-divergente",
            -108.00m, 108.00m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 1, 20), "aliq:g4-resgate-divergente",
            1300m, 1300m);

        await InserirCompraAsync(clienteId, instrumentoDivergente, "g4-compra-retroativa", new DateOnly(2025, 12, 25), 10m, 500m);

        var depoisDoPositivo = await ExecutarConciliacaoAsync();
        Assert.Equal(depoisDoControleNegativo.TributosDivergentesDoRederivado + 1, depoisDoPositivo.TributosDivergentesDoRederivado);
    }

    [Fact]
    public async Task Guarda4_ComCompraRegistradaDepoisDoResgateNoMesmoDataEvento_NaoAcusaPoisARederivacaoRespeitaOCortePorRegistradoEm()
    {
        var antes = await ExecutarConciliacaoAsync();

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await InserirCompraAsync(clienteId, instrumentoId, "g4-corte-compra-antiga", new DateOnly(2024, 1, 1), 5m, 500m);
        await InserirVendaAsync(clienteId, instrumentoId, "g4-corte-resgate", new DateOnly(2026, 6, 11), 10m, 1500m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido, new DateOnly(2026, 6, 11), "ir:g4-corte-resgate",
            -75.00m, 75.00m);
        await InserirAsync(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, new DateOnly(2026, 6, 11), "aliq:g4-corte-resgate",
            1500m, 1500m);
        await InserirCompraAsync(
            clienteId, instrumentoId, "g4-corte-compra-mesmo-dia", new DateOnly(2026, 6, 11), 5m, 700m,
            registradoEm: RegistradoEm.AddMinutes(1));

        var depois = await ExecutarConciliacaoAsync();
        Assert.Equal(antes.TributosDivergentesDoRederivado, depois.TributosDivergentesDoRederivado);
    }
}

using Custodia.Application.Common.Interfaces;
using Custodia.Application.Precos;
using Custodia.Application.Precos.Bootstrap;
using Custodia.Application.Precos.Hub;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Hub;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Custodia.Infrastructure.Tests.Calendario;
using Custodia.Infrastructure.Tests.Hub;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class BootstrapPrecosEndToEndIntegrationTests(InfrastructurePostgresFixture fixture)
{
    private sealed record HistoricoRow(
        string InstrumentoId, DateOnly DataRef, string Campo, string Fonte, decimal Valor, int Revisao, DateTimeOffset ObservadoEm);

    private sealed record PrecoAtualRow(string InstrumentoId, DateOnly DataRef, string Campo, decimal Valor, int Revisao);

    private static string NovoClienteId() => $"cli-e2e-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId(string sufixo) => $"td:e2e-{sufixo}-{Guid.NewGuid():N}";

    private static string NovaRefExterna() => $"ref-e2e-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => NpgsqlDataSource.Create(fixture.ConnectionString);

    private async Task<DateOnly> ObterHojeAsync()
    {
        await using var connection = await CriarDataSource().OpenConnectionAsync();
        var hoje = await connection.ExecuteScalarAsync<DateTime>("SELECT (now() AT TIME ZONE 'America/Sao_Paulo')::date");
        return DateOnly.FromDateTime(hoje);
    }

    private async Task InserirMovimentoAsync(string clienteId, string instrumentoId, DateOnly dataEvento)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);
        var movimento = Movimento.Create(
            clienteId, instrumentoId, TipoMovimento.Compra, dataEvento, DateTimeOffset.UtcNow, 10m, 1000m,
            NovaRefExterna()).Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);
    }

    private async Task PushHistoricoAsync(
        string instrumentoId, DateOnly dataRef, string campo, decimal valor, int revisao, DateTimeOffset observadoEm,
        string fonte = "td-e2e")
    {
        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        var observacao = new ObservacaoDePreco(instrumentoId, dataRef, campo, fonte, valor, revisao, observadoEm);
        var resultado = await repo.RegistrarHistoricoAsync(observacao, CancellationToken.None);
        Assert.True(resultado.IsSuccess);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);
    }

    private ColetarPrecosDoHubCommandHandler CriarHandler(FakeHubAsOfHandler fakeHub)
    {
        var httpClient = new HttpClient(fakeHub) { BaseAddress = new Uri("http://hub.fake/") };
        var hubClient = new HubPrecosClient(httpClient, NullLogger<HubPrecosClient>.Instance);
        var escopo = new EscopoDePrecosReadRepository(CriarDataSource());
        var calendario = new CalendarioDiasUteisReadRepository(CriarDataSource());
        var db = fixture.CriarDbContext();
        var precoWrite = new PrecoWriteRepository(db);
        var unitOfWork = (IUnitOfWork)db;
        var metrics = new FakeBusinessMetrics();
        var configuration = new ConfigurationBuilder().Build();

        return new ColetarPrecosDoHubCommandHandler(
            escopo, calendario, hubClient, precoWrite, unitOfWork, metrics, configuration,
            NullLogger<ColetarPrecosDoHubCommandHandler>.Instance);
    }

    private async Task<List<HistoricoRow>> LerHistoricoAsync(IEnumerable<string> instrumentoIds)
    {
        await using var connection = await CriarDataSource().OpenConnectionAsync();
        var rows = await connection.QueryAsync<HistoricoRow>(
            """
            SELECT instrumento_id AS "InstrumentoId", data_ref AS "DataRef", campo AS "Campo", fonte AS "Fonte",
                   valor AS "Valor", revisao AS "Revisao", observado_em AS "ObservadoEm"
            FROM historico_precos WHERE instrumento_id = ANY(@instrumentoIds)
            """,
            new { instrumentoIds = instrumentoIds.ToArray() });
        return rows.ToList();
    }

    private async Task<List<PrecoAtualRow>> LerPrecoAtualAsync(IEnumerable<string> instrumentoIds)
    {
        await using var connection = await CriarDataSource().OpenConnectionAsync();
        var rows = await connection.QueryAsync<PrecoAtualRow>(
            """
            SELECT instrumento_id AS "InstrumentoId", data_ref AS "DataRef", campo AS "Campo",
                   valor AS "Valor", revisao AS "Revisao"
            FROM preco_atual WHERE instrumento_id = ANY(@instrumentoIds)
            """,
            new { instrumentoIds = instrumentoIds.ToArray() });
        return rows.ToList();
    }

    private async Task TruncarProjecaoDePrecosAsync()
    {
        await using var connection = await CriarDataSource().OpenConnectionAsync();
        await connection.ExecuteAsync("TRUNCATE preco_atual, historico_precos");
    }

    [Fact]
    public async Task Bootstrap_ReconstroiAProjecaoDePrecosDepoisDeTruncate_ComDiffVazioNoRecorteEComplementoExato()
    {
        var hoje = await ObterHojeAsync();
        var clienteId = NovoClienteId();

        var instrumentoA = NovoInstrumentoId("revisao-substituida");
        var instrumentoB = NovoInstrumentoId("fora-do-livro");
        var instrumentoC = NovoInstrumentoId("data-ref-fora-da-janela");

        var desdeDaJanela = hoje.AddDays(-5);
        var ateDaJanela = hoje;

        await InserirMovimentoAsync(clienteId, instrumentoA, hoje.AddDays(-5));
        await InserirMovimentoAsync(clienteId, instrumentoC, hoje.AddDays(-5));

        var observadoEmBase = new DateTimeOffset(2020, 1, 1, 3, 0, 0, TimeSpan.Zero);

        await PushHistoricoAsync(instrumentoA, hoje.AddDays(-2), "pu_venda", 100m, 0, observadoEmBase);
        await PushHistoricoAsync(instrumentoA, hoje.AddDays(-2), "pu_venda", 110m, 1, observadoEmBase.AddHours(1));

        await PushHistoricoAsync(instrumentoB, hoje.AddDays(-1), "pu_venda", 50m, 0, observadoEmBase);

        await PushHistoricoAsync(instrumentoC, hoje.AddDays(-3), "pu_venda", 200m, 0, observadoEmBase);
        await PushHistoricoAsync(instrumentoC, hoje.AddDays(10), "pu_venda", 999m, 0, observadoEmBase);

        var fakeHub = new FakeHubAsOfHandler();
        fakeHub.RegistrarCatalogo(instrumentoA, "pu_venda");
        fakeHub.RegistrarCatalogo(instrumentoC, "pu_venda");
        fakeHub.RegistrarObservacao(new FakeHubAsOfHandler.ObservacaoDoCatalogo(
            instrumentoA, hoje.AddDays(-2), "pu_venda", "td-e2e", 100m, 0, observadoEmBase));
        fakeHub.RegistrarObservacao(new FakeHubAsOfHandler.ObservacaoDoCatalogo(
            instrumentoA, hoje.AddDays(-2), "pu_venda", "td-e2e", 110m, 1, observadoEmBase.AddHours(1)));
        fakeHub.RegistrarObservacao(new FakeHubAsOfHandler.ObservacaoDoCatalogo(
            instrumentoC, hoje.AddDays(-3), "pu_venda", "td-e2e", 200m, 0, observadoEmBase));
        fakeHub.RegistrarObservacao(new FakeHubAsOfHandler.ObservacaoDoCatalogo(
            instrumentoC, hoje.AddDays(10), "pu_venda", "td-e2e", 999m, 0, observadoEmBase));

        var handlerBootstrapInicial = CriarHandler(fakeHub);
        var resultadoInicial = await handlerBootstrapInicial.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, desdeDaJanela, ateDaJanela),
            CancellationToken.None);
        Assert.True(
            resultadoInicial.IsSuccess,
            resultadoInicial.IsFailure
                ? $"{resultadoInicial.Error.Code}: {resultadoInicial.Error.Description}"
                : string.Empty);

        var instrumentosDoLivro = new[] { instrumentoA, instrumentoC };
        var instrumentosTodos = new[] { instrumentoA, instrumentoB, instrumentoC };

        var historicoAntes = await LerHistoricoAsync(instrumentosTodos);
        var precoAtualAntes = await LerPrecoAtualAsync(instrumentosTodos);

        var recorteHistorico = historicoAntes
            .Where(h => instrumentosDoLivro.Contains(h.InstrumentoId))
            .Where(h => h.DataRef <= hoje)
            .GroupBy(h => (h.InstrumentoId, h.DataRef, h.Campo, h.Fonte))
            .Select(g => g.OrderByDescending(h => h.Revisao).First())
            .ToList();

        var complementoHistorico = historicoAntes.Except(recorteHistorico).ToList();

        Assert.NotEmpty(recorteHistorico);
        Assert.NotEmpty(complementoHistorico);
        Assert.Contains(complementoHistorico, h => h.InstrumentoId == instrumentoA && h.Revisao == 0);
        Assert.Contains(complementoHistorico, h => h.InstrumentoId == instrumentoB);
        Assert.Contains(complementoHistorico, h => h.InstrumentoId == instrumentoC && h.DataRef == hoje.AddDays(10));

        await TruncarProjecaoDePrecosAsync();
        Assert.Empty(await LerHistoricoAsync(instrumentosTodos));
        Assert.Empty(await LerPrecoAtualAsync(instrumentosTodos));

        var handlerBootstrapFinal = CriarHandler(fakeHub);
        var resultadoFinal = await handlerBootstrapFinal.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, desdeDaJanela, ateDaJanela),
            CancellationToken.None);
        Assert.True(
            resultadoFinal.IsSuccess,
            resultadoFinal.IsFailure
                ? $"{resultadoFinal.Error.Code}: {resultadoFinal.Error.Description}"
                : string.Empty);

        var historicoDepois = await LerHistoricoAsync(instrumentosTodos);
        var precoAtualDepois = await LerPrecoAtualAsync(instrumentosTodos);

        foreach (var linhaEsperada in recorteHistorico)
        {
            Assert.Contains(historicoDepois, h =>
                h.InstrumentoId == linhaEsperada.InstrumentoId
                && h.DataRef == linhaEsperada.DataRef
                && h.Campo == linhaEsperada.Campo
                && h.Fonte == linhaEsperada.Fonte
                && h.Valor == linhaEsperada.Valor
                && h.Revisao == linhaEsperada.Revisao
                && h.ObservadoEm.UtcDateTime == linhaEsperada.ObservadoEm.UtcDateTime);
        }

        Assert.Equal(recorteHistorico.Count, historicoDepois.Count);

        foreach (var linhaDoComplemento in complementoHistorico)
        {
            Assert.DoesNotContain(historicoDepois, h =>
                h.InstrumentoId == linhaDoComplemento.InstrumentoId
                && h.DataRef == linhaDoComplemento.DataRef
                && h.Campo == linhaDoComplemento.Campo
                && h.Fonte == linhaDoComplemento.Fonte
                && h.Revisao == linhaDoComplemento.Revisao);
        }

        Assert.Equal(precoAtualAntes.Count, precoAtualDepois.Count);
        foreach (var linhaEsperada in precoAtualAntes)
        {
            Assert.Contains(precoAtualDepois, p =>
                p.InstrumentoId == linhaEsperada.InstrumentoId
                && p.DataRef == linhaEsperada.DataRef
                && p.Campo == linhaEsperada.Campo
                && p.Valor == linhaEsperada.Valor
                && p.Revisao == linhaEsperada.Revisao);
        }

        Assert.DoesNotContain(precoAtualDepois, p => p.InstrumentoId == instrumentoB);
    }
}

using System.Data;
using Custodia.Application.Precos;
using Custodia.Infrastructure.Persistence.Repositories;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class PrecoWriteRepositoryTests(InfrastructurePostgresFixture fixture)
{
    static PrecoWriteRepositoryTests()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }

    private sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) =>
            new(DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc));

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTime;
            parameter.Value = value.UtcDateTime;
        }
    }

    private const string Fonte = "td-api";

    private static string NovoInstrumentoId() => $"td:preco-repo-{Guid.NewGuid():N}";

    private static ObservacaoDePreco Observacao(
        string instrumentoId, DateOnly dataRef, string campo, decimal valor, int revisao, DateTimeOffset observadoEm, string fonte = Fonte) =>
        new(instrumentoId, dataRef, campo, fonte, valor, revisao, observadoEm);

    private NpgsqlDataSource CriarDataSource() => NpgsqlDataSource.Create(fixture.ConnectionString);

    private async Task<List<HistoricoRow>> LerHistoricoAsync(string instrumentoId)
    {
        await using var connection = await CriarDataSource().OpenConnectionAsync();
        var rows = await connection.QueryAsync<HistoricoRow>(
            """
            SELECT instrumento_id AS "InstrumentoId", data_ref AS "DataRef", campo AS "Campo", fonte AS "Fonte",
                   valor AS "Valor", revisao AS "Revisao", observado_em AS "ObservadoEm"
            FROM historico_precos WHERE instrumento_id = @instrumentoId
            """,
            new { instrumentoId });
        return rows.ToList();
    }

    private async Task<PrecoAtualRow?> LerPrecoAtualAsync(string instrumentoId)
    {
        await using var connection = await CriarDataSource().OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<PrecoAtualRow>(
            """
            SELECT instrumento_id AS "InstrumentoId", data_ref AS "DataRef", campo AS "Campo",
                   valor AS "Valor", revisao AS "Revisao"
            FROM preco_atual WHERE instrumento_id = @instrumentoId
            """,
            new { instrumentoId });
    }

    private async Task<ResultadoPriceObservedTipo> PushAsync(PrecoWriteRepository repo, ObservacaoDePreco observacao)
    {
        var historicoResult = await repo.RegistrarHistoricoAsync(observacao, CancellationToken.None);
        Assert.True(historicoResult.IsSuccess);

        if (historicoResult.Value.Tipo == ResultadoHistoricoTipo.Divergente)
        {
            return ResultadoPriceObservedTipo.ValorDivergente;
        }

        if (historicoResult.Value.Tipo == ResultadoHistoricoTipo.ReplayInocuo)
        {
            return ResultadoPriceObservedTipo.ReplaySemAlteracao;
        }

        var atualizacaoResult = await repo.AtualizarPrecoAtualAsync(observacao, CancellationToken.None);
        Assert.True(atualizacaoResult.IsSuccess);

        return atualizacaoResult.Value.Tipo == ResultadoAtualizacaoPrecoAtualTipo.Atualizado
            ? ResultadoPriceObservedTipo.AplicadoEmPrecoAtual
            : ResultadoPriceObservedTipo.SoHistorico;
    }

    [Fact]
    public async Task RegistrarHistoricoAsync_ReentregaDoMesmoEvento_NaoDuplicaLinhaNemMudaNada()
    {
        var instrumentoId = NovoInstrumentoId();
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);
        var observacao = Observacao(instrumentoId, dataRef, "pu_venda", 3496.412345m, 0, observadoEm);

        await using var dbBootstrap = fixture.CriarDbContext();
        var repoBootstrap = new PrecoWriteRepository(dbBootstrap);
        await repoBootstrap.RegistrarBootstrapAsync(
            Observacao(instrumentoId, dataRef.AddDays(-1), "pu_venda", 1m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbBootstrap).SaveChangesAsync(CancellationToken.None);

        await using var db1 = fixture.CriarDbContext();
        var repo1 = new PrecoWriteRepository(db1);
        var primeiro = await PushAsync(repo1, observacao);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db1).SaveChangesAsync(CancellationToken.None);

        await using var db2 = fixture.CriarDbContext();
        var repo2 = new PrecoWriteRepository(db2);
        var segundo = await PushAsync(repo2, observacao);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db2).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.AplicadoEmPrecoAtual, primeiro);
        Assert.Equal(ResultadoPriceObservedTipo.ReplaySemAlteracao, segundo);

        var historico = await LerHistoricoAsync(instrumentoId);
        Assert.Equal(2, historico.Count);
        var linhaDaChaveEmpurrada = Assert.Single(historico, h => h.DataRef == dataRef);
        Assert.Equal(3496.412345m, linhaDaChaveEmpurrada.Valor);

        var precoAtual = await LerPrecoAtualAsync(instrumentoId);
        Assert.NotNull(precoAtual);
        Assert.Equal(3496.412345m, precoAtual!.Valor);
    }

    [Fact]
    public async Task RegistrarHistoricoAsync_MesmaChaveComValorDiferente_NadaSobrescritoEDesfechoNomeado()
    {
        var instrumentoId = NovoInstrumentoId();
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);
        var original = Observacao(instrumentoId, dataRef, "pu_venda", 3496.412345m, 0, observadoEm);
        var divergente = Observacao(instrumentoId, dataRef, "pu_venda", 4000.000000m, 0, observadoEm);

        await using var dbBootstrap = fixture.CriarDbContext();
        var repoBootstrap = new PrecoWriteRepository(dbBootstrap);
        await repoBootstrap.RegistrarBootstrapAsync(
            Observacao(instrumentoId, dataRef.AddDays(-1), "pu_venda", 1m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbBootstrap).SaveChangesAsync(CancellationToken.None);

        await using var db1 = fixture.CriarDbContext();
        var repo1 = new PrecoWriteRepository(db1);
        await PushAsync(repo1, original);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db1).SaveChangesAsync(CancellationToken.None);

        await using var db2 = fixture.CriarDbContext();
        var repo2 = new PrecoWriteRepository(db2);
        var resultado = await PushAsync(repo2, divergente);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db2).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.ValorDivergente, resultado);

        var historico = await LerHistoricoAsync(instrumentoId);
        Assert.Equal(2, historico.Count);
        var linha = Assert.Single(historico, h => h.DataRef == dataRef);
        Assert.Equal(3496.412345m, linha.Valor);

        var precoAtual = await LerPrecoAtualAsync(instrumentoId);
        Assert.NotNull(precoAtual);
        Assert.Equal(3496.412345m, precoAtual!.Valor);
    }

    [Fact]
    public async Task Push_MonotonicidadeDeDataRef_DPosteriorMudaEDMaisAntigaNao()
    {
        var instrumentoId = NovoInstrumentoId();
        var d1 = new DateOnly(2026, 8, 1);
        var d3 = new DateOnly(2026, 8, 3);
        var dMinus3 = new DateOnly(2026, 7, 29);
        var observadoEm = new DateTimeOffset(2026, 8, 3, 20, 0, 0, TimeSpan.Zero);

        await using var dbBootstrap = fixture.CriarDbContext();
        var repoBootstrap = new PrecoWriteRepository(dbBootstrap);
        await repoBootstrap.RegistrarBootstrapAsync(
            Observacao(instrumentoId, d1, "pu_venda", 100m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbBootstrap).SaveChangesAsync(CancellationToken.None);

        await using var dbAntiga = fixture.CriarDbContext();
        var repoAntiga = new PrecoWriteRepository(dbAntiga);
        var resultadoAntiga = await PushAsync(repoAntiga, Observacao(instrumentoId, dMinus3, "pu_venda", 90m, 0, observadoEm));
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbAntiga).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.SoHistorico, resultadoAntiga);
        var precoAtualAposAntiga = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal(d1, precoAtualAposAntiga!.DataRef);
        Assert.Equal(100m, precoAtualAposAntiga.Valor);

        await using var dbPosterior = fixture.CriarDbContext();
        var repoPosterior = new PrecoWriteRepository(dbPosterior);
        var resultadoPosterior = await PushAsync(repoPosterior, Observacao(instrumentoId, d3, "pu_venda", 110m, 0, observadoEm));
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbPosterior).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.AplicadoEmPrecoAtual, resultadoPosterior);
        var precoAtualAposPosterior = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal(d3, precoAtualAposPosterior!.DataRef);
        Assert.Equal(110m, precoAtualAposPosterior.Valor);

        var historico = await LerHistoricoAsync(instrumentoId);
        Assert.Equal(3, historico.Count);
    }

    [Fact]
    public async Task Push_MonotonicidadeDeRevisao_RevisaoZeroDepoisDeUmNaoMudaERevisaoUmMuda()
    {
        var instrumentoId = NovoInstrumentoId();
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await using var dbBootstrap = fixture.CriarDbContext();
        var repoBootstrap = new PrecoWriteRepository(dbBootstrap);
        await repoBootstrap.RegistrarBootstrapAsync(
            Observacao(instrumentoId, dataRef, "pu_venda", 100m, 1, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbBootstrap).SaveChangesAsync(CancellationToken.None);

        await using var dbRevisaoZero = fixture.CriarDbContext();
        var repoRevisaoZero = new PrecoWriteRepository(dbRevisaoZero);
        var resultadoZero = await PushAsync(repoRevisaoZero, Observacao(instrumentoId, dataRef, "pu_venda", 90m, 0, observadoEm));
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbRevisaoZero).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.SoHistorico, resultadoZero);
        var precoAtualAposZero = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal(1, precoAtualAposZero!.Revisao);
        Assert.Equal(100m, precoAtualAposZero.Valor);

        await using var dbRevisaoDois = fixture.CriarDbContext();
        var repoRevisaoDois = new PrecoWriteRepository(dbRevisaoDois);
        var resultadoDois = await PushAsync(repoRevisaoDois, Observacao(instrumentoId, dataRef, "pu_venda", 120m, 2, observadoEm));
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbRevisaoDois).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.AplicadoEmPrecoAtual, resultadoDois);
        var precoAtualAposDois = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal(2, precoAtualAposDois!.Revisao);
        Assert.Equal(120m, precoAtualAposDois.Valor);

        var historico = await LerHistoricoAsync(instrumentoId);
        Assert.Equal(3, historico.Count);
    }

    [Fact]
    public async Task Push_CampoDiferenteDoGravado_SoHistoricoENaoTocaPrecoAtual()
    {
        var instrumentoId = NovoInstrumentoId();
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await using var dbBootstrap = fixture.CriarDbContext();
        var repoBootstrap = new PrecoWriteRepository(dbBootstrap);
        await repoBootstrap.RegistrarBootstrapAsync(
            Observacao(instrumentoId, dataRef, "pu_venda", 100m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)dbBootstrap).SaveChangesAsync(CancellationToken.None);

        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        var resultado = await PushAsync(
            repo, Observacao(instrumentoId, new DateOnly(2026, 8, 5), "taxa_venda", 5.5m, 0, observadoEm));
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.SoHistorico, resultado);

        var precoAtual = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal("pu_venda", precoAtual!.Campo);
        Assert.Equal(100m, precoAtual.Valor);

        var historico = await LerHistoricoAsync(instrumentoId);
        Assert.Contains(historico, h => h.Campo == "taxa_venda" && h.Valor == 5.5m);
    }

    [Fact]
    public async Task Push_SemLinhaEmPrecoAtual_SoHistoricoEPrecoAtualContinuaSemLinha()
    {
        var instrumentoId = NovoInstrumentoId();
        var observacao = Observacao(
            instrumentoId, new DateOnly(2026, 8, 1), "pu_venda", 100m, 0, new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero));

        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        var resultado = await PushAsync(repo, observacao);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(ResultadoPriceObservedTipo.SoHistorico, resultado);
        Assert.Null(await LerPrecoAtualAsync(instrumentoId));
        Assert.Single(await LerHistoricoAsync(instrumentoId));
    }

    [Fact]
    public async Task RegistrarHistoricoAsync_ObservadoEmGravado_EODoPayloadNuncaODefaultNow()
    {
        var instrumentoId = NovoInstrumentoId();
        var observadoEmDoPayload = new DateTimeOffset(2020, 1, 1, 3, 0, 0, TimeSpan.Zero);
        var observacao = Observacao(instrumentoId, new DateOnly(2026, 8, 1), "pu_venda", 100m, 0, observadoEmDoPayload);

        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        await repo.RegistrarHistoricoAsync(observacao, CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);

        var historico = Assert.Single(await LerHistoricoAsync(instrumentoId));
        Assert.Equal(observadoEmDoPayload.UtcDateTime, historico.ObservadoEm.UtcDateTime);
        Assert.True((DateTimeOffset.UtcNow - historico.ObservadoEm) > TimeSpan.FromDays(365 * 5));
    }

    [Fact]
    public async Task RegistrarBootstrapAsync_SemLinhaExistente_CriaALinha()
    {
        var instrumentoId = NovoInstrumentoId();
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        var resultado = await repo.RegistrarBootstrapAsync(
            Observacao(instrumentoId, new DateOnly(2026, 8, 1), "pu_venda", 100m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoBootstrapPrecoAtualTipo.Criado, resultado.Value.PrecoAtual!.Tipo);

        var precoAtual = await LerPrecoAtualAsync(instrumentoId);
        Assert.NotNull(precoAtual);
        Assert.Equal("pu_venda", precoAtual!.Campo);
        Assert.Equal(100m, precoAtual.Valor);
    }

    [Fact]
    public async Task RegistrarBootstrapAsync_MesmoCampo_RespeitaAMonotonicidade()
    {
        var instrumentoId = NovoInstrumentoId();
        var observadoEm = new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero);

        await using var db1 = fixture.CriarDbContext();
        var repo1 = new PrecoWriteRepository(db1);
        await repo1.RegistrarBootstrapAsync(
            Observacao(instrumentoId, new DateOnly(2026, 8, 5), "pu_venda", 200m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db1).SaveChangesAsync(CancellationToken.None);

        await using var db2 = fixture.CriarDbContext();
        var repo2 = new PrecoWriteRepository(db2);
        var resultadoMaisVelho = await repo2.RegistrarBootstrapAsync(
            Observacao(instrumentoId, new DateOnly(2026, 8, 1), "pu_venda", 50m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db2).SaveChangesAsync(CancellationToken.None);

        Assert.True(resultadoMaisVelho.IsSuccess);
        Assert.Equal(ResultadoBootstrapPrecoAtualTipo.IgnoradoMaisAntigo, resultadoMaisVelho.Value.PrecoAtual!.Tipo);

        var precoAtual = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal(new DateOnly(2026, 8, 5), precoAtual!.DataRef);
        Assert.Equal(200m, precoAtual.Valor);
    }

    [Fact]
    public async Task RegistrarBootstrapAsync_CampoDiferente_TrocaMesmoComDataRefMaisVelha_ESinalizaATroca()
    {
        var instrumentoId = NovoInstrumentoId();
        var observadoEm = new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero);

        await using var db1 = fixture.CriarDbContext();
        var repo1 = new PrecoWriteRepository(db1);
        await repo1.RegistrarBootstrapAsync(
            Observacao(instrumentoId, new DateOnly(2026, 8, 5), "pu_venda", 200m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db1).SaveChangesAsync(CancellationToken.None);

        await using var db2 = fixture.CriarDbContext();
        var repo2 = new PrecoWriteRepository(db2);
        var resultadoTroca = await repo2.RegistrarBootstrapAsync(
            Observacao(instrumentoId, new DateOnly(2026, 7, 1), "taxa_venda", 5m, 0, observadoEm), CancellationToken.None);
        await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db2).SaveChangesAsync(CancellationToken.None);

        Assert.True(resultadoTroca.IsSuccess);
        Assert.Equal(ResultadoBootstrapPrecoAtualTipo.CampoTrocado, resultadoTroca.Value.PrecoAtual!.Tipo);
        Assert.Equal("pu_venda", resultadoTroca.Value.PrecoAtual.CampoAnterior);

        var precoAtual = await LerPrecoAtualAsync(instrumentoId);
        Assert.Equal("taxa_venda", precoAtual!.Campo);
        Assert.Equal(new DateOnly(2026, 7, 1), precoAtual.DataRef);
        Assert.Equal(5m, precoAtual.Valor);
    }

    private sealed record HistoricoRow(
        string InstrumentoId, DateOnly DataRef, string Campo, string Fonte, decimal Valor, int Revisao, DateTimeOffset ObservadoEm);

    private sealed record PrecoAtualRow(string InstrumentoId, DateOnly DataRef, string Campo, decimal Valor, int Revisao);
}

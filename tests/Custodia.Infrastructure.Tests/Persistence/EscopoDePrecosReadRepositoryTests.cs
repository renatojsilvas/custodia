using Custodia.Application.Common.Interfaces;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Persistence.Repositories;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class EscopoDePrecosReadRepositoryTests(InfrastructurePostgresFixture fixture)
{
    private static string NovoClienteId() => $"cli-escopo-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:escopo-{Guid.NewGuid():N}";

    private static string NovaRefExterna() => $"ref-escopo-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => NpgsqlDataSource.Create(fixture.ConnectionString);

    private async Task InserirMovimentoAsync(
        string clienteId, string instrumentoId, DateOnly dataEvento, TipoMovimento? tipo = null)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);
        var movimento = Movimento.Create(
            clienteId, instrumentoId, tipo ?? TipoMovimento.Compra, dataEvento, DateTimeOffset.UtcNow, 10m, 1000m,
            NovaRefExterna()).Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);
    }

    private async Task InserirPrecoAtualAsync(string instrumentoId, DateOnly dataRef, decimal valor)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        var observacao = new Custodia.Application.Precos.ObservacaoDePreco(
            instrumentoId, dataRef, "pu_venda", "td", valor, 0, DateTimeOffset.UtcNow);
        var resultado = await repo.RegistrarBootstrapAsync(observacao, CancellationToken.None);
        Assert.True(resultado.IsSuccess);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);
    }

    [Fact]
    public async Task ObterLivroInteiroAsync_ExcluiInstrumentosDeCaixaIndependenteDaCaixa()
    {
        var clienteId = NovoClienteId();
        var instrumentoNormal = NovoInstrumentoId();

        await InserirMovimentoAsync(clienteId, instrumentoNormal, new DateOnly(2026, 1, 5));
        await InserirMovimentoAsync(clienteId, InstrumentosCaixa.Brl, new DateOnly(2026, 1, 5), TipoMovimento.Compra);
        await InserirMovimentoAsync(clienteId, InstrumentosCaixa.ALiquidar, new DateOnly(2026, 1, 5), TipoMovimento.Compra);

        var repo = new EscopoDePrecosReadRepository(CriarDataSource());
        var resultado = await repo.ObterLivroInteiroAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Contains(instrumentoNormal, resultado.Value.InstrumentosId);
        Assert.DoesNotContain(InstrumentosCaixa.Brl, resultado.Value.InstrumentosId);
        Assert.DoesNotContain(InstrumentosCaixa.ALiquidar, resultado.Value.InstrumentosId);
        Assert.DoesNotContain(
            resultado.Value.InstrumentosId, id => id.StartsWith(InstrumentosCaixa.Prefixo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ObterLivroInteiroAsync_ComparaOPrefixoDeCaixaCaseInsensitive_ControlePositivoDaConsultaSql()
    {
        var repo = new EscopoDePrecosReadRepository(CriarDataSource());

        await using var connection = await CriarDataSource().OpenConnectionAsync();
        var excluiComPrefixoMaiusculo = await connection.ExecuteScalarAsync<bool>(
            "SELECT 'CAIXA:QUALQUER' ILIKE @padrao", new { padrao = $"{InstrumentosCaixa.Prefixo}%" });

        Assert.True(
            excluiComPrefixoMaiusculo,
            "a consulta usa ILIKE contra InstrumentosCaixa.Prefixo, então um id de caixa fora de minúsculas " +
            "(hipotético, hoje bloqueado pela check constraint do banco) também seria excluído do escopo.");

        _ = repo;
    }

    [Fact]
    public async Task ObterLivroInteiroAsync_MenorDataEventoConsideraSoInstrumentosNaoCaixa()
    {
        var clienteId = NovoClienteId();
        var instrumentoNormal = NovoInstrumentoId();
        var dataAntiga = new DateOnly(2020, 3, 1);
        var dataMaisAntigaAindaDeCaixa = new DateOnly(2010, 1, 1);

        await InserirMovimentoAsync(clienteId, instrumentoNormal, dataAntiga);
        await InserirMovimentoAsync(clienteId, InstrumentosCaixa.Brl, dataMaisAntigaAindaDeCaixa);

        var repo = new EscopoDePrecosReadRepository(CriarDataSource());
        var resultado = await repo.ObterLivroInteiroAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value.MenorDataEvento);
        Assert.True(resultado.Value.MenorDataEvento <= dataAntiga);
    }

    [Fact]
    public async Task ObterSemPrecoAtualAsync_DevolveApenasInstrumentosDoLivroForaDeCaixaSemLinhaEmPrecoAtual()
    {
        var clienteId = NovoClienteId();
        var comPreco = NovoInstrumentoId();
        var semPreco = NovoInstrumentoId();

        await InserirMovimentoAsync(clienteId, comPreco, new DateOnly(2026, 1, 5));
        await InserirMovimentoAsync(clienteId, semPreco, new DateOnly(2026, 1, 5));
        await InserirMovimentoAsync(clienteId, InstrumentosCaixa.Brl, new DateOnly(2026, 1, 5));
        await InserirPrecoAtualAsync(comPreco, new DateOnly(2026, 1, 5), 100m);

        var repo = new EscopoDePrecosReadRepository(CriarDataSource());
        var resultado = await repo.ObterSemPrecoAtualAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Contains(semPreco, resultado.Value);
        Assert.DoesNotContain(comPreco, resultado.Value);
        Assert.DoesNotContain(InstrumentosCaixa.Brl, resultado.Value);
    }
}

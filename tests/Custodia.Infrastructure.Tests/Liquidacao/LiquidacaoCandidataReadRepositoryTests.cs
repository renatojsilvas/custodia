using Custodia.Application.Common.Interfaces;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Custodia.Infrastructure.Tests.Persistence;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Liquidacao;

[Collection("infra-postgres")]
public sealed class LiquidacaoCandidataReadRepositoryTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateOnly DataEvento = new(2026, 8, 10);
    private static readonly DateTimeOffset RegistradoEm = new(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);

    private static string NovoClienteId() => $"cli-candidata-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:candidata-{Guid.NewGuid():N}";

    private LiquidacaoCandidataReadRepository CriarRepositorio() => new(fixture.DataSource);

    private async Task<Movimento> InserirAsync(
        string clienteId, string instrumentoId, TipoMovimento tipo, string refExterna,
        decimal qtdDelta, decimal valorFinanceiro, long? refEstorno = null)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);

        var movimento = Movimento.Create(
            clienteId, instrumentoId, tipo, DataEvento, RegistradoEm, qtdDelta, valorFinanceiro, refExterna, refEstorno).Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        return movimento;
    }

    private Task<Movimento> InserirVendaAsync(string clienteId, string instrumentoId, string tradeId) =>
        InserirAsync(clienteId, instrumentoId, TipoMovimento.Venda, tradeId, -10m, 1200m);

    private Task<Movimento> InserirAliqAsync(string clienteId, string tradeId) =>
        InserirAsync(clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, $"aliq:{tradeId}", 1200m, 1200m);

    [Fact]
    public async Task ObterAbertasNaoRevertidasAsync_ResgateSimplesSemLiquidacao_ApareceComoCandidata()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-candidata-simples";

        await InserirVendaAsync(clienteId, instrumentoId, tradeId);
        await InserirAliqAsync(clienteId, tradeId);

        var repo = CriarRepositorio();
        var resultado = await repo.ObterAbertasNaoRevertidasAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Contains(resultado.Value, c => c.ClienteId == clienteId && c.TradeId == tradeId && c.DataEvento == DataEvento);
    }

    [Fact]
    public async Task ObterAbertasNaoRevertidasAsync_ResgateJaLiquidado_NaoApareceMais()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-candidata-liquidada";

        await InserirVendaAsync(clienteId, instrumentoId, tradeId);
        await InserirAliqAsync(clienteId, tradeId);
        await InserirAsync(clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao, $"liq:{tradeId}:aliq", -1200m, 1200m);
        await InserirAsync(clienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao, $"liq:{tradeId}:brl", 1200m, 1200m);

        var repo = CriarRepositorio();
        var resultado = await repo.ObterAbertasNaoRevertidasAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.DoesNotContain(resultado.Value, c => c.ClienteId == clienteId && c.TradeId == tradeId);
    }

    [Fact]
    public async Task ObterAbertasNaoRevertidasAsync_ALiquidarComAjusteRevertendo_NaoAparece()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-candidata-revertida";

        await InserirVendaAsync(clienteId, instrumentoId, tradeId);
        var aliq = await InserirAliqAsync(clienteId, tradeId);
        await InserirAsync(clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Ajuste, $"est:aliq:op-estorno", -1200m, -1200m, refEstorno: aliq.Id);

        var repo = CriarRepositorio();
        var resultado = await repo.ObterAbertasNaoRevertidasAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.DoesNotContain(resultado.Value, c => c.ClienteId == clienteId && c.TradeId == tradeId);
    }

    [Fact]
    public async Task ObterAbertasNaoRevertidasAsync_MovimentoPrincipalRevertidoMasALiquidarIntacto_NaoAparece()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var tradeId = "op-candidata-principal-revertido";

        var venda = await InserirVendaAsync(clienteId, instrumentoId, tradeId);
        await InserirAliqAsync(clienteId, tradeId);
        await InserirAsync(clienteId, instrumentoId, TipoMovimento.Ajuste, "est:op-estorno-legado", 10m, -1200m, refEstorno: venda.Id);

        var repo = CriarRepositorio();
        var resultado = await repo.ObterAbertasNaoRevertidasAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.DoesNotContain(resultado.Value, c => c.ClienteId == clienteId && c.TradeId == tradeId);
    }

    [Fact]
    public async Task ObterAbertasNaoRevertidasAsync_SoTrazLinhasDeTipoALiquidar()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await InserirAsync(clienteId, instrumentoId, TipoMovimento.Compra, "op-compra-irrelevante", 10m, 1000m);

        var repo = CriarRepositorio();
        var resultado = await repo.ObterAbertasNaoRevertidasAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.DoesNotContain(resultado.Value, c => c.ClienteId == clienteId);
    }
}

using Custodia.Application.Common.Interfaces;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class MovimentoTravamentoRepositoryTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateOnly DataEvento = new(2026, 8, 10);
    private static readonly DateTimeOffset RegistradoEm = new(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);

    private static string NovoClienteId() => $"cli-travamento-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:travamento-{Guid.NewGuid():N}";

    private async Task<Movimento> InserirAliqAsync(string clienteId, string tradeId)
    {
        await using (var dbVenda = fixture.CriarDbContext())
        {
            var repoVenda = new MovimentoWriteRepository(dbVenda);

            var venda = Movimento.Create(
                clienteId, NovoInstrumentoId(), TipoMovimento.Venda, DataEvento, RegistradoEm, -10m, 1000m, tradeId).Value;

            await repoVenda.AdicionarAsync(venda, CancellationToken.None);
            var salvouVenda = await ((IUnitOfWork)dbVenda).SaveChangesAsync(CancellationToken.None);
            Assert.True(salvouVenda.IsSuccess);
        }

        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);

        var movimento = Movimento.Create(
            clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, DataEvento, RegistradoEm,
            1000m, 1000m, $"aliq:{tradeId}").Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        return movimento;
    }

    [Fact]
    public async Task TravarPorClienteERefExternaAsync_SegundaChamadaNaMesmaLinha_BloqueiaAteAPrimeiraTransacaoTerminar()
    {
        var clienteId = NovoClienteId();
        var tradeId = "op-travamento-1";
        await InserirAliqAsync(clienteId, tradeId);

        await using var dbPrimeiraTransacao = fixture.CriarDbContext();
        var repoPrimeiraTransacao = new MovimentoTravamentoRepository(dbPrimeiraTransacao);

        var primeiroTravamento = await repoPrimeiraTransacao.TravarPorClienteERefExternaAsync(
            clienteId, $"aliq:{tradeId}", CancellationToken.None);
        Assert.True(primeiroTravamento.IsSuccess);
        Assert.True(primeiroTravamento.Value.Encontrado);

        await using var dbSegundaTransacao = fixture.CriarDbContext();
        var repoSegundaTransacao = new MovimentoTravamentoRepository(dbSegundaTransacao);

        var segundaChamada = repoSegundaTransacao.TravarPorClienteERefExternaAsync(
            clienteId, $"aliq:{tradeId}", CancellationToken.None);

        var terminouCedoDemais = await Task.WhenAny(segundaChamada, Task.Delay(TimeSpan.FromMilliseconds(500))) == segundaChamada;
        Assert.False(terminouCedoDemais, "a segunda chamada não deveria completar enquanto a primeira transação mantém a linha travada.");

        await ((IUnitOfWork)dbPrimeiraTransacao).DescartarTransacaoAsync(CancellationToken.None);

        var segundoTravamento = await segundaChamada.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(segundoTravamento.IsSuccess);
        Assert.True(segundoTravamento.Value.Encontrado);
    }

    [Fact]
    public async Task TravarPorClienteERefExternaAsync_LinhaInexistente_DevolveNaoEncontradoSemBloquear()
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoTravamentoRepository(db);

        var resultado = await repo.TravarPorClienteERefExternaAsync(
            NovoClienteId(), $"aliq:{NovoInstrumentoId()}", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value.Encontrado);
    }

    [Fact]
    public async Task TravarPorClienteERefExternaAsync_ApósSaveChanges_LiberaOTravamentoParaOProximo()
    {
        var clienteId = NovoClienteId();
        var tradeId = "op-travamento-2";
        await InserirAliqAsync(clienteId, tradeId);

        await using (var db = fixture.CriarDbContext())
        {
            var repo = new MovimentoTravamentoRepository(db);
            var travamento = await repo.TravarPorClienteERefExternaAsync(clienteId, $"aliq:{tradeId}", CancellationToken.None);
            Assert.True(travamento.Value.Encontrado);

            var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
            Assert.True(salvou.IsSuccess);
        }

        await using var dbSeguinte = fixture.CriarDbContext();
        var repoSeguinte = new MovimentoTravamentoRepository(dbSeguinte);

        var travamentoSeguinte = await repoSeguinte
            .TravarPorClienteERefExternaAsync(clienteId, $"aliq:{tradeId}", CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(travamentoSeguinte.IsSuccess);
        Assert.True(travamentoSeguinte.Value.Encontrado);
    }
}

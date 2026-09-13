using System.Text;
using Custodia.Application.Common.Interfaces;
using Custodia.Domain.Eventos;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Messaging;
using Custodia.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Custodia.Infrastructure.Tests.Messaging;

[Collection("rabbitmq-consumidor")]
public sealed class ParkingDrenadorTests(RabbitMqConsumidorFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public Task InitializeAsync() => fixture.LimparEstadoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DrenarAsync_DoisMotivosDiferentesNaMesmaFila_DrenaUmEDevolveOOutroAoFimDaFila()
    {
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();

        await PlantarAsync(canalAux, "trades.registered", "{}", MotivoParking.TipoNaoTratadoPrices.Name);
        await PlantarAsync(canalAux, "corpactions.evento", "{}", MotivoParking.TipoNaoTratadoCorpactions.Name);
        await EsperarContagemAsync(canalAux, 2);

        var (drenador, _) = CriarDrenador();

        var resultado = await drenador.DrenarAsync(
            MotivoParking.TipoNaoTratadoPrices, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Completude, resultado.Desfecho);
        Assert.Equal(1, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);

        var motivosRestantes = await LerMotivosDaFilaAsync(canalAux);
        Assert.Equal([MotivoParking.TipoNaoTratadoCorpactions.Name], motivosRestantes);
    }

    [Fact]
    public async Task DrenarAsync_Completude_ExaminaAsNSemResidualComPeloMenosUmaProcessada_PublicaOResidualNaMetrica()
    {
        var motivo = MotivoParking.PayloadInvalido;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name);
        await EsperarContagemAsync(canalAux, 1);

        var (drenador, metrics) = CriarDrenador();

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Completude, resultado.Desfecho);
        Assert.Equal(1, resultado.Estoque);
        Assert.Equal(1, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);
        Assert.Equal(0d, metrics.LerResidualPorMotivo(motivo.Name));
    }

    [Fact]
    public async Task DrenarAsync_HandlerRejeitaDePropositoAMensagemDoMotivoPedido_DevolveParcialMesmoComNMotivoZero()
    {
        var motivo = MotivoParking.VersaoNaoSuportada;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name);
        await EsperarContagemAsync(canalAux, 1);

        var reprocessador = new FakeMensagemParkingReprocessador((_, _) => ResultadoReprocessamento.Falha(motivo));
        var (drenador, metrics) = CriarDrenador(reprocessador);

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Parcial, resultado.Desfecho);
        Assert.Equal(0, resultado.NMotivo);
        Assert.Equal(1, resultado.ResidualMotivo);
        Assert.Equal(1d, metrics.LerResidualPorMotivo(motivo.Name));

        var motivosRestantes = await LerMotivosDaFilaAsync(canalAux);
        Assert.Equal([motivo.Name], motivosRestantes);
    }

    [Fact]
    public async Task DrenarAsync_FilaVazia_DevolveVazioDoMotivo()
    {
        var motivo = MotivoParking.EstornoDuplicado;
        var (drenador, metrics) = CriarDrenador();

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.VazioDoMotivo, resultado.Desfecho);
        Assert.Equal(0, resultado.Estoque);
        Assert.Equal(0, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);
        Assert.Equal(0d, metrics.LerResidualPorMotivo(motivo.Name));
    }

    [Fact]
    public async Task DrenarAsync_FilaSoComOutrosMotivos_DevolveVazioDoMotivoMesmoComEstoqueMaiorQueZero()
    {
        var motivoPedido = MotivoParking.EstornoDivergente;
        var outroMotivo = MotivoParking.OrigemRecursoInvalida;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "trades.registered", "{}", outroMotivo.Name);
        await EsperarContagemAsync(canalAux, 1);

        var (drenador, metrics) = CriarDrenador();

        var resultado = await drenador.DrenarAsync(motivoPedido, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.VazioDoMotivo, resultado.Desfecho);
        Assert.Equal(1, resultado.Estoque);
        Assert.Equal(0, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);
        Assert.Equal(0d, metrics.LerResidualPorMotivo(motivoPedido.Name));

        var motivosRestantes = await LerMotivosDaFilaAsync(canalAux);
        Assert.Equal([outroMotivo.Name], motivosRestantes);
    }

    [Fact]
    public async Task DrenarAsync_EstoqueAcimaDoTeto_DevolveLimitePorTetoENaoPublicaAMetrica()
    {
        var motivo = MotivoParking.RetryIndisponivel;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name);
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name);
        await EsperarContagemAsync(canalAux, 2);

        var (drenador, metrics) = CriarDrenador(
            configExtras: new Dictionary<string, string?> { ["Decisao:ParkingDrenagemTeto"] = "1" });
        metrics.RegistrarResidualPorMotivo(motivo.Name, 42);

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.LimitePorTeto, resultado.Desfecho);
        Assert.Equal(2, resultado.Estoque);
        Assert.Equal(1, resultado.Teto);
        Assert.Equal(42d, metrics.LerResidualPorMotivo(motivo.Name));

        var restantes = await canalAux.MessageCountAsync(RabbitMqTopologia.FilaParked);
        Assert.Equal(2u, restantes);
    }

    [Fact]
    public async Task DrenarAsync_MensagemJaCarimbadaComAPassagemCorrente_DevolveLimitePorVoltaENaoPublicaAMetrica()
    {
        var motivo = MotivoParking.OrigemRecursoAusente;
        const string passagemFixa = "passagem-fixa-teste-de-volta";
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name, passagemId: passagemFixa);
        await EsperarContagemAsync(canalAux, 1);

        var (drenador, metrics) = CriarDrenador();
        metrics.RegistrarResidualPorMotivo(motivo.Name, 7);

        var resultado = await drenador.DrenarAsync(motivo, passagemFixa, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.LimitePorVolta, resultado.Desfecho);
        Assert.Equal(passagemFixa, resultado.PassagemId);
        Assert.Equal(7d, metrics.LerResidualPorMotivo(motivo.Name));
    }

    [Fact]
    public async Task DrenarAsync_FilaPurgadaEntreALeituraDoEstoqueEOConsumo_DevolveInterrompidaENaoPublicaAMetrica()
    {
        var motivo = MotivoParking.OrigemRecursoInvalida;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name);
        await EsperarContagemAsync(canalAux, 1);

        var gancho = new FuncPontoDeSuspensaoDrenagem(async ct =>
        {
            await using var conexaoPurga = await fixture.CriarConexaoAmqpAsync();
            await using var canalPurga = await conexaoPurga.CreateChannelAsync();
            await canalPurga.QueuePurgeAsync(RabbitMqTopologia.FilaParked, ct);
        });

        var (drenador, metrics) = CriarDrenador(gancho: gancho);
        metrics.RegistrarResidualPorMotivo(motivo.Name, 13);

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Interrompida, resultado.Desfecho);
        Assert.Equal(1, resultado.Estoque);
        Assert.Equal(0, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);
        Assert.Equal(13d, metrics.LerResidualPorMotivo(motivo.Name));
    }

    [Fact]
    public async Task DrenarAsync_ComSondaDeDeployDoF2EUmaMensagemDoMotivo_DescontaASondaDeNEDevolveCompletude()
    {
        var motivo = MotivoParking.IdentificadorComEspacoNaBorda;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "custodia-f2-topologia", "custodia-f2-sonda-abortada", motivo: null);
        await PlantarAsync(canalAux, "trades.registered", "{}", motivo.Name);
        await EsperarContagemAsync(canalAux, 2);

        var (drenador, _) = CriarDrenador();

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Completude, resultado.Desfecho);
        Assert.Equal(2, resultado.Estoque);
        Assert.Equal(1, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);

        var restantes = await canalAux.MessageCountAsync(RabbitMqTopologia.FilaParked);
        Assert.Equal(0u, restantes);
    }

    [Fact]
    public async Task DrenarAsync_ApenasComSondaDeDeployDoF2_DevolveVazioDoMotivoNuncaSucessoPorLixo()
    {
        var motivo = MotivoParking.EstornoClienteDivergente;
        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "custodia-f2-topologia", "custodia-f2-sonda-solitaria", motivo: null);
        await EsperarContagemAsync(canalAux, 1);

        var (drenador, _) = CriarDrenador();

        var resultado = await drenador.DrenarAsync(motivo, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.VazioDoMotivo, resultado.Desfecho);
        Assert.Equal(1, resultado.Estoque);
        Assert.Equal(0, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);

        var restantes = await canalAux.MessageCountAsync(RabbitMqTopologia.FilaParked);
        Assert.Equal(0u, restantes);
    }

    [Fact]
    public async Task DrenarAsync_ComOReprocessadorDeProducao_EstornoOrfaoComOOriginalJaNoLivro_EscrituraEDevolveCompletude()
    {
        var configuration = fixture.CriarConfiguration();
        var provider = fixture.CriarServiceProvider(configuration);

        var clienteId = $"cli-drenador-{Guid.NewGuid():N}";
        const string instrumentoId = "td:tesouro-drenador";
        var tradeIdOriginal = $"trade-{Guid.NewGuid():N}";
        var tradeIdEstorno = $"estorno-{Guid.NewGuid():N}";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        await using (var db = fixture.CriarDbContext())
        {
            var movimentoWrite = new MovimentoWriteRepository(db);
            var original = Movimento.Create(
                clienteId, instrumentoId, TipoMovimento.Compra, dataEvento, registradoEm, 10m, 1000m, tradeIdOriginal).Value;
            await movimentoWrite.AdicionarAsync(original, CancellationToken.None);
            var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
            Assert.True(salvou.IsSuccess);
        }

        var estornoPayload = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 10m, 1000m, dataEvento, registradoEm, tradeIdOriginal);

        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(
            canalAux, "trades.registered", estornoPayload, MotivoParking.EstornoOrfaoExpirado.Name);
        await EsperarContagemAsync(canalAux, 1);

        var drenador = new ParkingDrenador(
            provider.GetRequiredService<RabbitMqConnectionProvider>(),
            provider.GetRequiredService<IPublicadorComConfirmacao>(),
            provider.GetRequiredService<IMensagemParkingReprocessador>(),
            provider.GetRequiredService<IIdentificadorDePassagem>(),
            provider.GetRequiredService<IPontoDeSuspensaoDrenagem>(),
            new ParkingDrenadorMetrics(),
            configuration,
            provider.GetRequiredService<ILogger<ParkingDrenador>>());

        var resultado = await drenador.DrenarAsync(
            MotivoParking.EstornoOrfaoExpirado, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Completude, resultado.Desfecho);
        Assert.Equal(1, resultado.NMotivo);
        Assert.Equal(0, resultado.ResidualMotivo);

        await using var dbVerificacao = fixture.CriarDbContext();
        var ajusteGravado = await dbVerificacao.Movimentos.AnyAsync(
            m => m.ClienteId == clienteId && m.RefExterna == tradeIdEstorno);
        Assert.True(ajusteGravado);
    }

    private (ParkingDrenador Drenador, ParkingDrenadorMetrics Metrics) CriarDrenador(
        IMensagemParkingReprocessador? reprocessador = null,
        IPontoDeSuspensaoDrenagem? gancho = null,
        IDictionary<string, string?>? configExtras = null)
    {
        var configuration = fixture.CriarConfiguration(configExtras);
        var provider = fixture.CriarServiceProvider(configuration);
        var connectionProvider = provider.GetRequiredService<RabbitMqConnectionProvider>();
        var publicador = provider.GetRequiredService<IPublicadorComConfirmacao>();
        var metrics = new ParkingDrenadorMetrics();
        var logger = provider.GetRequiredService<ILogger<ParkingDrenador>>();

        var drenador = new ParkingDrenador(
            connectionProvider,
            publicador,
            reprocessador ?? new FakeMensagemParkingReprocessador(),
            new IdentificadorDePassagemAleatorio(),
            gancho ?? new PontoDeSuspensaoDrenagemInerte(),
            metrics,
            configuration,
            logger);

        return (drenador, metrics);
    }

    private static async Task PlantarAsync(
        IChannel canal, string routingKey, string corpo, string? motivo, string? passagemId = null)
    {
        var headers = new Dictionary<string, object?>();
        if (motivo is not null)
        {
            headers[RabbitMqCabecalhos.Motivo] = motivo;
        }

        if (passagemId is not null)
        {
            headers[RabbitMqCabecalhos.PassagemId] = passagemId;
        }

        var propriedades = new BasicProperties();
        if (headers.Count > 0)
        {
            propriedades.Headers = headers;
        }

        await canal.BasicPublishAsync(
            RabbitMqTopologia.ExchangeParking, routingKey, mandatory: false, propriedades, Encoding.UTF8.GetBytes(corpo));
    }

    private static async Task<List<string>> LerMotivosDaFilaAsync(IChannel canal)
    {
        var motivos = new List<string>();
        while (true)
        {
            var entrega = await canal.BasicGetAsync(RabbitMqTopologia.FilaParked, autoAck: true);
            if (entrega is null)
            {
                break;
            }

            var motivo = RabbitMqCabecalhos.LerMotivo(entrega.BasicProperties.Headers);
            if (motivo is not null)
            {
                motivos.Add(motivo);
            }
        }

        return motivos;
    }

    private static async Task EsperarContagemAsync(IChannel canal, uint esperado)
    {
        var inicio = DateTime.UtcNow;
        while (DateTime.UtcNow - inicio < Timeout)
        {
            if (await canal.MessageCountAsync(RabbitMqTopologia.FilaParked) == esperado)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        Assert.Fail($"a custodia.parked não atingiu {esperado} mensagens dentro do timeout.");
    }
}

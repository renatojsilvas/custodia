using System.Text;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Precos;
using Custodia.Domain.Eventos;
using Custodia.Infrastructure.Messaging;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;
using RabbitMQ.Client;

namespace Custodia.Infrastructure.Tests.Messaging;

[Collection("rabbitmq-consumidor")]
public sealed class ParkingDrenadorPrecoTests(RabbitMqConsumidorFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private static readonly Counter RevisaoDePrecoRecebidaTotal = Metrics.CreateCounter(
        "custodia_preco_revisao_recebida_total", "help", new CounterConfiguration { LabelNames = ["instrumento_id", "campo"] });

    public Task InitializeAsync() => fixture.LimparEstadoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DrenarAsync_TipoNaoTratadoPricesComPriceObservedValidoPlantado_DevolveCompletudeEGravaOHistorico()
    {
        var instrumentoId = $"td:drenador-preco-{Guid.NewGuid():N}";
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);
        var payload = PriceObservedPayloadBuilder.Valido(instrumentoId, dataRef, "pu_venda", 3496.412345m, "td-api", 0, observadoEm);

        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "prices.td", payload, MotivoParking.TipoNaoTratadoPrices.Name);
        await EsperarContagemAsync(canalAux, 1);

        var (drenador, provider) = CriarDrenadorDeProducao();
        await using var _ = provider;

        var resultado = await drenador.DrenarAsync(MotivoParking.TipoNaoTratadoPrices, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Completude, resultado.Desfecho);
        Assert.True(resultado.NMotivo >= 1);
        Assert.Equal(0, resultado.ResidualMotivo);

        await using var db = fixture.CriarDbContext();
        var linhas = await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*)::int AS \"Value\" FROM historico_precos WHERE instrumento_id = {instrumentoId}")
            .SingleAsync();
        Assert.Equal(1, linhas);
    }

    [Fact]
    public async Task DrenarAsync_PricesComOutroTipoSobOMotivoDePrices_FicaComoResidual_ControleDeQueNaoAprovaTudo()
    {
        var instrumentoId = $"td:drenador-preco-residual-{Guid.NewGuid():N}";
        var payloadDeOutroTipo = PriceObservedPayloadBuilder.ComOutroTipo(instrumentoId);

        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "prices.td", payloadDeOutroTipo, MotivoParking.TipoNaoTratadoPrices.Name);
        await EsperarContagemAsync(canalAux, 1);

        var (drenador, provider) = CriarDrenadorDeProducao();
        await using var _ = provider;

        var resultado = await drenador.DrenarAsync(MotivoParking.TipoNaoTratadoPrices, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Parcial, resultado.Desfecho);
        Assert.Equal(0, resultado.NMotivo);
        Assert.Equal(1, resultado.ResidualMotivo);

        var restantes = await canalAux.MessageCountAsync(RabbitMqTopologia.FilaParked);
        Assert.Equal(1u, restantes);
    }

    [Fact]
    public async Task DrenarAsync_RevisaoMaiorQueZeroDrenada_SubstituiOValorEmPrecoAtualEDeixaAsDuasRevisoesNoHistorico()
    {
        var instrumentoId = $"td:drenador-preco-revisao-{Guid.NewGuid():N}";
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await using (var dbBootstrap = fixture.CriarDbContext())
        {
            var repoBootstrap = new PrecoWriteRepository(dbBootstrap);
            await repoBootstrap.RegistrarBootstrapAsync(
                new ObservacaoDePreco(instrumentoId, dataRef, "pu_venda", "td-api", 100m, 0, observadoEm),
                CancellationToken.None);
            await ((IUnitOfWork)dbBootstrap).SaveChangesAsync(CancellationToken.None);
        }

        var payloadRevisao = PriceObservedPayloadBuilder.Valido(instrumentoId, dataRef, "pu_venda", 150m, "td-api", 1, observadoEm);

        await using var conexaoAux = await fixture.CriarConexaoAmqpAsync();
        await using var canalAux = await conexaoAux.CreateChannelAsync();
        await PlantarAsync(canalAux, "prices.td", payloadRevisao, MotivoParking.TipoNaoTratadoPrices.Name);
        await EsperarContagemAsync(canalAux, 1);

        var antes = RevisaoDePrecoRecebidaTotal.WithLabels(instrumentoId, "pu_venda").Value;

        var (drenador, provider) = CriarDrenadorDeProducao();
        await using var _ = provider;

        var resultado = await drenador.DrenarAsync(MotivoParking.TipoNaoTratadoPrices, null, CancellationToken.None);

        Assert.Equal(DesfechoDrenagem.Completude, resultado.Desfecho);

        var depois = RevisaoDePrecoRecebidaTotal.WithLabels(instrumentoId, "pu_venda").Value;
        Assert.Equal(1, depois - antes);

        await using var db = fixture.CriarDbContext();
        var precoAtual = await db.Database.SqlQuery<PrecoAtualRow>(
                $"SELECT valor AS \"Valor\", revisao AS \"Revisao\" FROM preco_atual WHERE instrumento_id = {instrumentoId}")
            .SingleAsync();
        Assert.Equal(150m, precoAtual.Valor);
        Assert.Equal(1, precoAtual.Revisao);

        var linhasDeHistorico = await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*)::int AS \"Value\" FROM historico_precos WHERE instrumento_id = {instrumentoId}")
            .SingleAsync();
        Assert.Equal(2, linhasDeHistorico);
    }

    private sealed record PrecoAtualRow(decimal Valor, int Revisao);

    private (ParkingDrenador Drenador, ServiceProvider Provider) CriarDrenadorDeProducao()
    {
        var configuration = fixture.CriarConfiguration();
        var provider = fixture.CriarServiceProvider(configuration);

        var drenador = new ParkingDrenador(
            provider.GetRequiredService<RabbitMqConnectionProvider>(),
            provider.GetRequiredService<IPublicadorComConfirmacao>(),
            provider.GetRequiredService<IMensagemParkingReprocessador>(),
            provider.GetRequiredService<IIdentificadorDePassagem>(),
            provider.GetRequiredService<IPontoDeSuspensaoDrenagem>(),
            new ParkingDrenadorMetrics(),
            configuration,
            provider.GetRequiredService<ILogger<ParkingDrenador>>());

        return (drenador, provider);
    }

    private static async Task PlantarAsync(IChannel canal, string routingKey, string corpo, string motivo)
    {
        var headers = new Dictionary<string, object?> { [RabbitMqCabecalhos.Motivo] = motivo };
        var propriedades = new BasicProperties { Headers = headers };

        await canal.BasicPublishAsync(
            RabbitMqTopologia.ExchangeParking, routingKey, mandatory: false, propriedades, Encoding.UTF8.GetBytes(corpo));
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

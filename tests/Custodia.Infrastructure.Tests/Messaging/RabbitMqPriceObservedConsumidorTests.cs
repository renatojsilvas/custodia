using System.Diagnostics;
using System.Text;
using Custodia.Application.Precos;
using Custodia.Domain.Eventos;
using Custodia.Infrastructure.Messaging;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Custodia.Infrastructure.Tests.Messaging;

[Collection("rabbitmq-consumidor")]
public sealed class RabbitMqPriceObservedConsumidorTests(RabbitMqConsumidorFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan TimeoutCurto = TimeSpan.FromSeconds(20);

    public Task InitializeAsync() => fixture.LimparEstadoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PriceObservedPublicadoEmPricesTd_ComPrecoAtualSemeado_AtualizaPrecoAtualEOHistorico()
    {
        const string instrumentoId = "td:tesouro-teste-price-push";
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await SemearPrecoAtualAsync(instrumentoId, dataRef.AddDays(-1), "pu_venda", 1m, 0, observadoEm);

        var payload = PriceObservedPayloadBuilder.Valido(instrumentoId, dataRef, "pu_venda", 3496.412345m, "td-api", 0, observadoEm);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "prices.td", payload);

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await EsperarAsync(async () =>
            {
                var precoAtual = await LerPrecoAtualAsync(instrumentoId);
                return precoAtual is not null && precoAtual.Valor == 3496.412345m;
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var historico = await ContarHistoricoAsync(instrumentoId, dataRef);
        Assert.Equal(1, historico);
    }

    [Fact]
    public async Task PriceObservedComPayloadInvalido_EstacionaComPayloadInvalido()
    {
        const string instrumentoId = "td:tesouro-teste-price-invalido";
        var payload = "{\"v\":1,\"tipo\":\"PriceObserved\",\"instrumentoId\":\"" + instrumentoId + "\"}";

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "prices.td", payload);

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var motivosAcumulados = new HashSet<string>();
            Assert.True(await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosAcumulados.Contains(MotivoParking.PayloadInvalido.Name);
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PriceObservedComRevisaoMaiorQueZero_SubstituiOValorEmPrecoAtualEDeixaAsDuasRevisoesNoHistorico()
    {
        const string instrumentoId = "td:tesouro-teste-price-revisao";
        var dataRef = new DateOnly(2026, 8, 1);
        var observadoEm = new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

        await SemearPrecoAtualAsync(instrumentoId, dataRef, "pu_venda", 100m, 0, observadoEm);

        var payloadRevisao = PriceObservedPayloadBuilder.Valido(instrumentoId, dataRef, "pu_venda", 150m, "td-api", 1, observadoEm);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "prices.td", payloadRevisao);

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await EsperarAsync(async () =>
            {
                var precoAtual = await LerPrecoAtualAsync(instrumentoId);
                return precoAtual is not null && precoAtual.Valor == 150m && precoAtual.Revisao == 1;
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var historico = await ContarHistoricoAsync(instrumentoId, dataRef);
        Assert.Equal(2, historico);
    }

    private async Task SemearPrecoAtualAsync(
        string instrumentoId, DateOnly dataRef, string campo, decimal valor, int revisao, DateTimeOffset observadoEm)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new PrecoWriteRepository(db);
        await repo.RegistrarBootstrapAsync(
            new ObservacaoDePreco(instrumentoId, dataRef, campo, "td-api", valor, revisao, observadoEm), CancellationToken.None);
        var salvou = await ((Custodia.Application.Common.Interfaces.IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);
    }

    private sealed record PrecoAtualRow(decimal Valor, int Revisao);

    private async Task<PrecoAtualRow?> LerPrecoAtualAsync(string instrumentoId)
    {
        await using var db = fixture.CriarDbContext();
        return await db.Database.SqlQuery<PrecoAtualRow>(
                $"SELECT valor AS \"Valor\", revisao AS \"Revisao\" FROM preco_atual WHERE instrumento_id = {instrumentoId}")
            .SingleOrDefaultAsync();
    }

    private async Task<int> ContarHistoricoAsync(string instrumentoId, DateOnly dataRef)
    {
        await using var db = fixture.CriarDbContext();
        return await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*)::int AS \"Value\" FROM historico_precos WHERE instrumento_id = {instrumentoId} AND data_ref = {dataRef}")
            .SingleAsync();
    }

    private (ServiceProvider Provider, RabbitMqTradeConsumidor Consumidor, ConsumidorMetrics Metrics) CriarConsumidor()
    {
        var configuration = fixture.CriarConfiguration();
        var provider = fixture.CriarServiceProvider(configuration);
        var connectionProvider = provider.GetRequiredService<RabbitMqConnectionProvider>();
        var publicador = provider.GetRequiredService<IPublicadorComConfirmacao>();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var roteador = provider.GetRequiredService<Custodia.Application.Eventos.RoteadorDeEventos>();
        var metrics = provider.GetRequiredService<ConsumidorMetrics>();
        var logger = provider.GetRequiredService<ILogger<RabbitMqTradeConsumidor>>();

        var consumidor = new RabbitMqTradeConsumidor(
            connectionProvider, publicador, scopeFactory, roteador, metrics, configuration, logger);

        return (provider, consumidor, metrics);
    }

    private async Task<List<string>> LerMotivosDaFilaParkedAsync()
    {
        await using var conexao = await fixture.CriarConexaoAmqpAsync();
        await using var canal = await conexao.CreateChannelAsync();

        var motivos = new List<string>();
        while (true)
        {
            var entrega = await canal.BasicGetAsync(RabbitMqTopologia.FilaParked, autoAck: true);
            if (entrega is null)
            {
                break;
            }

            if (entrega.BasicProperties.Headers is { } headers
                && headers.TryGetValue(RabbitMqCabecalhos.Motivo, out var motivoBruto))
            {
                motivos.Add(ParaTexto(motivoBruto));
            }
        }

        return motivos;
    }

    private static string ParaTexto(object? valor) => valor switch
    {
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string texto => texto,
        _ => valor?.ToString() ?? string.Empty,
    };

    private static async Task PublicarAsync(IChannel canal, string routingKey, string corpo)
    {
        var propriedades = new BasicProperties();
        await canal.BasicPublishAsync(
            RabbitMqTopologia.ExchangePrices, routingKey, mandatory: false, propriedades, Encoding.UTF8.GetBytes(corpo));
    }

    private static async Task<bool> EsperarAsync(Func<Task<bool>> condicao, TimeSpan timeout)
    {
        var cronometro = Stopwatch.StartNew();
        while (cronometro.Elapsed < timeout)
        {
            if (await condicao())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        return await condicao();
    }
}

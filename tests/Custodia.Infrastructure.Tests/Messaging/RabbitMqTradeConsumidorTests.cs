using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Custodia.Application.Eventos;
using Custodia.Domain.Common;
using Custodia.Domain.Eventos;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Messaging;
using Custodia.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Prometheus;
using RabbitMQ.Client;

namespace Custodia.Infrastructure.Tests.Messaging;

[Collection("rabbitmq-consumidor")]
public sealed class RabbitMqTradeConsumidorTests(RabbitMqConsumidorFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan TimeoutCurto = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan TimeoutComUmaVoltaDeRetry = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan TimeoutComDuasVoltasDeRetry = TimeSpan.FromSeconds(90);

    private static readonly Counter MensagensPorDesfechoTotal = Metrics.CreateCounter(
        "custodia_consumo_mensagens_total", "help", new CounterConfiguration { LabelNames = ["desfecho"] });

    private static double LerContadorDesfecho(string desfecho) => MensagensPorDesfechoTotal.WithLabels(desfecho).Value;

    public Task InitializeAsync() => fixture.LimparEstadoAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DecisaoA_OrfaoNaFrenteDoOriginal_OriginalEProcessadoEAjusteAplicadoSemUpdate()
    {
        var clienteId = NovoId("cli");
        const string instrumentoId = "td:tesouro-teste-a";
        var tradeIdOriginal = NovoId("trade");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 10m, 1000m, dataEvento, registradoEm, tradeIdOriginal);
        var original = TradePayloadBuilder.Aplicacao(
            tradeIdOriginal, clienteId, instrumentoId, 10m, 1000m, dataEvento, registradoEm);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);
        await PublicarAsync(canalPublicador, "trades.registered", original);

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var originalGravado = await EsperarAsync(
                async () => await ExisteMovimentoAsync(tradeIdOriginal), TimeoutCurto);

            Assert.True(
                originalGravado,
                "o TradeRegistered original deveria ser processado ENQUANTO o estorno órfão espera no " +
                "custodia.retry — com nack(requeue:true) este teste travaria aqui.");

            var ajusteGravado = await EsperarAsync(
                async () => await ExisteMovimentoAsync(tradeIdEstorno), TimeoutComUmaVoltaDeRetry);

            Assert.True(ajusteGravado, "o ajuste do estorno órfão deveria ser gravado após a volta pelo custodia.retry");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        await using var db = fixture.CriarDbContext();
        var linhas = await db.Movimentos.Where(m => m.ClienteId == clienteId).OrderBy(m => m.Id).ToListAsync();

        Assert.Equal(2, linhas.Count);
        var linhaOriginal = linhas.Single(l => l.RefExterna == tradeIdOriginal);
        var linhaAjuste = linhas.Single(l => l.RefExterna == tradeIdEstorno);
        Assert.Equal(10m, linhaOriginal.QtdDelta);
        Assert.Equal(1000m, linhaOriginal.ValorFinanceiro);
        Assert.Equal(linhaOriginal.Id, linhaAjuste.RefEstorno);
        Assert.Equal(-linhaOriginal.QtdDelta, linhaAjuste.QtdDelta);
        Assert.Equal(-linhaOriginal.ValorFinanceiro, linhaAjuste.ValorFinanceiro);
    }

    [Fact]
    public async Task DecisaoA_TetoEAlcancavel_UmaVoltaDoOrfaoChegaComXCustodiaVoltasUmEASegundaComDois()
    {
        var clienteId = NovoId("cli");
        const string instrumentoId = "td:tesouro-teste-b";
        var tradeIdInexistente = NovoId("trade-inexistente");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 5m, 500m, dataEvento, registradoEm, tradeIdInexistente);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);

        var (provider, consumidor, publicador) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var chegouComUmaVolta = await EsperarAsync(
                () => Task.FromResult(publicador.Chamadas.Any(c =>
                    c.Exchange == RabbitMqTopologia.ExchangeRetryIn &&
                    RabbitMqCabecalhos.LerVoltas(c.Cabecalhos) == 1)),
                TimeoutComUmaVoltaDeRetry);

            Assert.True(chegouComUmaVolta, "a primeira volta deveria chegar com x-custodia-voltas == 1");

            var chegouComDuasVoltas = await EsperarAsync(
                () => Task.FromResult(publicador.Chamadas.Any(c =>
                    c.Exchange == RabbitMqTopologia.ExchangeRetryIn &&
                    RabbitMqCabecalhos.LerVoltas(c.Cabecalhos) == 2)),
                TimeoutComDuasVoltasDeRetry);

            Assert.True(
                chegouComDuasVoltas,
                "a SEGUNDA volta do mesmo órfão deveria chegar com x-custodia-voltas == 2 — " +
                "se o republish não copiar os headers recebidos e incrementar o contador próprio, esse número " +
                "fica 1 para sempre (o x-death do broker fica travado em 1, medido contra o rabbitmq:4-management-alpine).");

            Assert.False(await ExisteMovimentoAsync(tradeIdEstorno), "o órfão não pode gravar nada enquanto não é resolvido");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DecisaoA_TetoBaixoConfiguradoEEstourado_OrfaoEstacionaComoEstornoOrfaoExpiradoENuncaNaDlq()
    {
        const int teto = 2;
        var clienteId = NovoId("cli");
        const string instrumentoId = "td:tesouro-teste-teto-baixo";
        var tradeIdInexistente = NovoId("trade-inexistente");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 4m, 400m, dataEvento, registradoEm, tradeIdInexistente);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);

        var (provider, consumidor, publicador) = CriarConsumidor(
            configExtras: new Dictionary<string, string?> { ["Decisao:OrfaoTetoVoltas"] = teto.ToString() });
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var motivosAcumulados = new HashSet<string>();
            var estacionadoComoOrfaoExpirado = await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosAcumulados.Contains(MotivoParking.EstornoOrfaoExpirado.Name);
            }, TimeoutComDuasVoltasDeRetry);

            Assert.True(
                estacionadoComoOrfaoExpirado,
                "com o teto de voltas configurado em 2, o órfão que não se resolve tem que estacionar como " +
                "estorno_orfao_expirado — nunca circular para sempre e nunca cair na DLQ");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var voltasRealizadas = publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeRetryIn);
        Assert.Equal(teto, voltasRealizadas);

        Assert.False(await ExisteMovimentoAsync(tradeIdEstorno));

        var profundidadeDlq = await ContarMensagensAsync(RabbitMqTopologia.FilaDlq);
        Assert.Equal(0u, profundidadeDlq);
    }

    [Fact]
    public async Task DecisaoA_ConfirmPerdidoAposEntregaReal_DuplicaOOrfaoNoRetryEAmbasParkeiamSemCorromperOLivro()
    {
        const int teto = 1;
        var clienteId = NovoId("cli");
        const string instrumentoId = "td:tesouro-teste-confirm-perdido";
        var tradeIdInexistente = NovoId("trade-inexistente");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 3m, 300m, dataEvento, registradoEm, tradeIdInexistente);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);

        var (provider, consumidor, publicador) = CriarConsumidor(
            confirmar: (exchange, tentativa) => !(exchange == RabbitMqTopologia.ExchangeRetryIn && tentativa == 1),
            entregaMesmoComConfirmNegado: (exchange, tentativa) =>
                exchange == RabbitMqTopologia.ExchangeRetryIn && tentativa == 1,
            configExtras: new Dictionary<string, string?> { ["Decisao:OrfaoTetoVoltas"] = teto.ToString() });
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var duasPublicacoesNoRetryIn = await EsperarAsync(
                () => Task.FromResult(publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeRetryIn) >= 2),
                TimeoutCurto);

            Assert.True(
                duasPublicacoesNoRetryIn,
                "a primeira tentativa (confirm negado, mas ENTREGUE de verdade ao custodia.retry.in) e a segunda " +
                "tentativa (confirm concedido, republish real por causa do nack da original) juntas produzem DUAS " +
                "cópias do mesmo órfão circulando — o 'sim parcial' do confirm.");

            var motivosAcumulados = new List<string>();
            var duasParkedComoOrfaoExpirado = await EsperarAsync(async () =>
            {
                motivosAcumulados.AddRange(await LerMotivosDaFilaParkedAsync());
                return motivosAcumulados.Count(m => m == MotivoParking.EstornoOrfaoExpirado.Name) >= 2;
            }, TimeoutComDuasVoltasDeRetry);

            Assert.True(
                duasParkedComoOrfaoExpirado,
                "as duas cópias do órfão, cada uma reprocessada de forma independente após o TTL do " +
                "custodia.retry, têm que estacionar como estorno_orfao_expirado — duplicata no parking é o limite " +
                "conhecido deste 'sim parcial', NUNCA uma escrita no livro.");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        Assert.False(
            await ExisteMovimentoAsync(tradeIdEstorno),
            "o confirm perdido não pode, em nenhuma das duas cópias, resultar em escrita no livro");

        var profundidadeRetry = await ContarMensagensAsync(RabbitMqTopologia.FilaRetry);
        Assert.Equal(0u, profundidadeRetry);

        var profundidadeDlq = await ContarMensagensAsync(RabbitMqTopologia.FilaDlq);
        Assert.Equal(0u, profundidadeDlq);
    }

    [Fact]
    public async Task DecisaoA_AckSoDepoisDoConfirm_ConfirmForcadoAFalharUmaVez_OriginalNaoConfirmadaEDepoisSim()
    {
        var clienteId = NovoId("cli");
        const string instrumentoId = "td:tesouro-teste-b2";
        var tradeIdInexistente = NovoId("trade-inexistente");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 2m, 200m, dataEvento, registradoEm, tradeIdInexistente);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);

        var (provider, consumidor, publicador) = CriarConsumidor(
            confirmar: (exchange, tentativa) => !(exchange == RabbitMqTopologia.ExchangeRetryIn && tentativa == 1));
        await using var _ = provider;

        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var segundaTentativaOcorreu = await EsperarAsync(
                () => Task.FromResult(publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeRetryIn) >= 2),
                TimeoutCurto);

            Assert.True(
                segundaTentativaOcorreu,
                "com o confirm da primeira tentativa forçado a falhar, a original tem que ser reentregue " +
                "(nack com requeue) e reprocessada — é isso que produz a segunda tentativa.");

            var publicadoDeVerdade = await EsperarAsync(
                async () => await ContarMensagensAsync(RabbitMqTopologia.FilaRetry) >= 1,
                TimeoutCurto);

            Assert.True(
                publicadoDeVerdade,
                "a SEGUNDA tentativa, com confirm liberado, tem que resultar num publish real na custodia.retry — " +
                "prova de que a primeira tentativa (confirm negado) não chegou a ser entregue de verdade.");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DecisaoB_TresDesfechosDeEstornoNoMesmoTeste_CadaUmComMotivoDistinguivel()
    {
        const string instrumentoId = "td:tesouro-teste-b3";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var clienteDono = NovoId("cli-dono");
        var clienteErrado = NovoId("cli-errado");
        var tradeDivergenteCliente = NovoId("trade-div-cli");
        var estornoDivergenteCliente = NovoId("estorno-div-cli");

        var clienteVersao = NovoId("cli-versao");
        var tradeVersao = NovoId("trade-versao");

        var clienteCamposDivergentes = NovoId("cli-campos");
        var tradeCamposDivergentes = NovoId("trade-campos");
        var estornoCamposDivergentes = NovoId("estorno-campos");

        var clienteControlePositivo = NovoId("cli-ctrl");
        var tradeControlePositivo = NovoId("trade-ctrl");
        var estornoControlePositivo = NovoId("estorno-ctrl");

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeDivergenteCliente, clienteDono, instrumentoId, 10m, 1000m, dataEvento, registradoEm));
        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeCamposDivergentes, clienteCamposDivergentes, instrumentoId, 10m, 1000m, dataEvento, registradoEm));
        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeControlePositivo, clienteControlePositivo, instrumentoId, 7m, 700m, dataEvento, registradoEm));

        var (provider, consumidor, publicador) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await EsperarAsync(async () =>
                await ExisteMovimentoAsync(tradeDivergenteCliente)
                && await ExisteMovimentoAsync(tradeCamposDivergentes)
                && await ExisteMovimentoAsync(tradeControlePositivo), TimeoutCurto));

            await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Estorno(
                estornoDivergenteCliente, clienteErrado, instrumentoId, 10m, 1000m, dataEvento, registradoEm, tradeDivergenteCliente));
            await PublicarAsync(canalPublicador, "trades.registered",
                TradePayloadBuilder.ComVersaoNaoSuportada(tradeVersao, clienteVersao, instrumentoId));
            await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Estorno(
                estornoCamposDivergentes, clienteCamposDivergentes, instrumentoId, 999m, 1000m, dataEvento, registradoEm, tradeCamposDivergentes));
            await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Estorno(
                estornoControlePositivo, clienteControlePositivo, instrumentoId, 7m, 700m, dataEvento, registradoEm, tradeControlePositivo));

            Assert.True(
                await EsperarAsync(async () => await ExisteMovimentoAsync(estornoControlePositivo), TimeoutCurto),
                "controle positivo: o mesmo estorno com os três campos batendo tem que gravar o ajuste normalmente");

            var motivosEsperados = new HashSet<string>
            {
                MotivoParking.EstornoClienteDivergente.Name,
                MotivoParking.VersaoNaoSuportada.Name,
                MotivoParking.EstornoDivergente.Name,
            };

            var motivosAcumulados = new HashSet<string>();
            Assert.True(await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosEsperados.All(motivosAcumulados.Contains);
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        Assert.DoesNotContain(publicador.Chamadas, c => c.Exchange == RabbitMqTopologia.ExchangeRetryIn);

        await using var db = fixture.CriarDbContext();
        Assert.False(await db.Movimentos.AnyAsync(m => m.RefExterna == estornoDivergenteCliente));
        Assert.False(await db.Movimentos.AnyAsync(m => m.RefExterna == estornoCamposDivergentes));
    }

    [Fact]
    public async Task DecisaoB_SegundoEstornoDoMesmoMovimento_EstacionaComoEstornoDuplicadoSemNadaGravarESemCairNaDlq()
    {
        const string instrumentoId = "td:tesouro-teste-b3-duplicado";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var clienteId = NovoId("cli-dup");
        var tradeId = NovoId("trade-dup");
        var estorno1 = NovoId("estorno-dup-1");
        var estorno2 = NovoId("estorno-dup-2");

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeId, clienteId, instrumentoId, 10m, 1000m, dataEvento, registradoEm));

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await EsperarAsync(() => ExisteMovimentoAsync(tradeId), TimeoutCurto));

            await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Estorno(
                estorno1, clienteId, instrumentoId, 10m, 1000m, dataEvento, registradoEm, tradeId));
            await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Estorno(
                estorno2, clienteId, instrumentoId, 10m, 1000m, dataEvento, registradoEm, tradeId));

            var motivosAcumulados = new HashSet<string>();
            Assert.True(await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosAcumulados.Contains(MotivoParking.EstornoDuplicado.Name);
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        Assert.True(await ExisteMovimentoAsync(estorno1));
        Assert.False(await ExisteMovimentoAsync(estorno2));

        var profundidadeDlq = await ContarMensagensAsync(RabbitMqTopologia.FilaDlq);
        Assert.Equal(0u, profundidadeDlq);
    }

    [Fact]
    public async Task DecisaoA_ConfirmNegadoPersistente_ParaNoTetoDeTentativasEEstacionaComoRetryIndisponivel()
    {
        const int teto = 3;
        var clienteId = NovoId("cli");
        const string instrumentoId = "td:tesouro-teste-b4";
        var tradeIdInexistente = NovoId("trade-inexistente");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 3m, 300m, dataEvento, registradoEm, tradeIdInexistente);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);

        var (provider, consumidor, publicador) = CriarConsumidor(
            confirmar: (exchange, _) => exchange != RabbitMqTopologia.ExchangeRetryIn,
            configExtras: new Dictionary<string, string?> { ["Decisao:RetryTetoTentativas"] = teto.ToString() });
        await using var _ = provider;

        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var motivosAcumulados = new HashSet<string>();
            var estacionadoComoRetryIndisponivel = await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosAcumulados.Contains(MotivoParking.RetryIndisponivel.Name);
            }, TimeoutCurto);

            Assert.True(
                estacionadoComoRetryIndisponivel,
                "esperava a mensagem estacionar como retry_indisponivel após esgotar o teto de tentativas de republish");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var tentativasParaRetryIn = publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeRetryIn);
        Assert.Equal(teto, tentativasParaRetryIn);

        Assert.False(await ExisteMovimentoAsync(tradeIdEstorno));

        var profundidadeDlq = await ContarMensagensAsync(RabbitMqTopologia.FilaDlq);
        Assert.Equal(0u, profundidadeDlq);
    }

    [Fact]
    public async Task PADROES_10_48_ConfirmNegadoAoPublicarRetry_DesfechoDeFalhaDeParkingNaoTransitorio_EJaLimitadoPeloTeto()
    {
        const int teto = 3;
        var clienteId = NovoId("cli-instancia-b");
        const string instrumentoId = "td:tesouro-teste-10-48-instancia-b";
        var tradeIdInexistente = NovoId("trade-inexistente");
        var tradeIdEstorno = NovoId("estorno");
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var estornoOrfao = TradePayloadBuilder.Estorno(
            tradeIdEstorno, clienteId, instrumentoId, 3m, 300m, dataEvento, registradoEm, tradeIdInexistente);

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", estornoOrfao);

        var antesFalhaDeRetry = LerContadorDesfecho("nack_requeue_falha_ao_publicar_retry");
        var antesTransitorio = LerContadorDesfecho("nack_requeue_transitorio");

        var (provider, consumidor, publicador) = CriarConsumidor(
            confirmar: (exchange, _) => exchange != RabbitMqTopologia.ExchangeRetryIn,
            configExtras: new Dictionary<string, string?> { ["Decisao:RetryTetoTentativas"] = teto.ToString() });
        await using var _ = provider;

        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var motivosAcumulados = new HashSet<string>();
            var estacionadoComoRetryIndisponivel = await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosAcumulados.Contains(MotivoParking.RetryIndisponivel.Name);
            }, TimeoutCurto);

            Assert.True(
                estacionadoComoRetryIndisponivel,
                "o x-acquired-count desta entrega já limita o número de tentativas de publicar em custodia.retry.in " +
                "ANTES de tentar publicar — o laço é finito por construção, e tem que terminar em retry_indisponivel.");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var tentativasParaRetryIn = publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeRetryIn);
        Assert.Equal(teto, tentativasParaRetryIn);

        var depoisFalhaDeRetry = LerContadorDesfecho("nack_requeue_falha_ao_publicar_retry");
        var depoisTransitorio = LerContadorDesfecho("nack_requeue_transitorio");

        Assert.True(
            depoisFalhaDeRetry > antesFalhaDeRetry,
            "o publisher confirm negado ao publicar em custodia.retry.in não tem para onde mandar a mensagem — " +
            "requeue é a única saída, mas o desfecho tem que dizer que foi a REPUBLICAÇÃO EM RETRY que falhou.");
        Assert.True(
            antesTransitorio == depoisTransitorio,
            "o confirm negado ao publicar em retry NUNCA pode ser contabilizado como nack_requeue_transitorio — " +
            "a métrica mentiria sobre a causa.");

        Assert.False(await ExisteMovimentoAsync(tradeIdEstorno));

        var profundidadeDlq = await ContarMensagensAsync(RabbitMqTopologia.FilaDlq);
        Assert.Equal(0u, profundidadeDlq);
    }

    [Fact]
    public async Task Bijecao_ConjuntoConhecidoDeEventos_TodosGravadosENenhumExtra_ComControlePositivoDeLinhaPlantada()
    {
        const string instrumentoId = "td:tesouro-teste-bijecao";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var esperados = Enumerable.Range(0, 5)
            .Select(i => (ClienteId: NovoId($"cli-bij-{i}"), TradeId: NovoId($"trade-bij-{i}")))
            .ToList();

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        foreach (var (clienteId, tradeId) in esperados)
        {
            await PublicarAsync(canalPublicador, "trades.registered",
                TradePayloadBuilder.Aplicacao(tradeId, clienteId, instrumentoId, 1m, 100m, dataEvento, registradoEm));
        }

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await EsperarAsync(async () =>
            {
                var refs = esperados.Select(e => e.TradeId).ToList();
                await using var db = fixture.CriarDbContext();
                return await db.Movimentos.CountAsync(m => refs.Contains(m.RefExterna)) == esperados.Count;
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        await VarrerBijecaoAsync(esperados);

        await using (var db = fixture.CriarDbContext())
        {
            var linhaPlantada = Movimento.Create(
                esperados[0].ClienteId,
                instrumentoId,
                TipoMovimento.Compra,
                dataEvento,
                registradoEm,
                qtdDelta: 1m,
                valorFinanceiro: 100m,
                refExterna: NovoId("linha-plantada-fora-do-conjunto")).Value;

            db.Movimentos.Add(linhaPlantada);
            await db.SaveChangesAsync();
        }

        var falhaEsperadaComALinhaPlantada = await Record.ExceptionAsync(() => VarrerBijecaoAsync(esperados));

        Assert.NotNull(falhaEsperadaComALinhaPlantada);
    }

    [Fact]
    public async Task PrimeiroBoot_DrenaBacklogAcumulado_FilaCheiaEOCasoNormal()
    {
        const string instrumentoId = "td:tesouro-teste-fila-cheia";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var eventos = Enumerable.Range(0, 8)
            .Select(i => (ClienteId: NovoId($"cli-cheia-{i}"), TradeId: NovoId($"trade-cheia-{i}")))
            .ToList();

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        foreach (var (clienteId, tradeId) in eventos)
        {
            await PublicarAsync(canalPublicador, "trades.registered",
                TradePayloadBuilder.Aplicacao(tradeId, clienteId, instrumentoId, 2m, 200m, dataEvento, registradoEm));
        }

        var filaEstavaCheiaAntesDoBoot = await EsperarAsync(
            async () => await canalPublicador.MessageCountAsync(RabbitMqTopologia.FilaPrincipal) >= (uint)eventos.Count,
            TimeoutCurto);
        Assert.True(
            filaEstavaCheiaAntesDoBoot,
            "a fila deveria estar CHEIA (backlog) ANTES de o consumidor subir — este é o caso NORMAL do primeiro boot, não o excepcional");

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await EsperarAsync(async () =>
            {
                var refs = eventos.Select(e => e.TradeId).ToList();
                await using var db = fixture.CriarDbContext();
                return await db.Movimentos.CountAsync(m => refs.Contains(m.RefExterna)) == eventos.Count;
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var profundidadeDepois = await canalPublicador.MessageCountAsync(RabbitMqTopologia.FilaPrincipal);
        Assert.Equal(0u, profundidadeDepois);
    }

    [Fact]
    public async Task Sonda_PricesSmokeComPrefixoDeSonda_EAckadaSemEstacionar_ComControlePositivo()
    {
        const string instrumentoId = "td:tesouro-teste-sonda";

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        var sondaTexto = $"custodia-f2-smoke-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        await PublicarAsync(canalPublicador, "prices.smoke", sondaTexto);

        var payloadDeContrato = "{\"instrumentoId\":\"" + instrumentoId + "\",\"preco\":\"100.00\"}";
        await PublicarAsync(canalPublicador, "prices.smoke", payloadDeContrato);

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

                return motivosAcumulados.Contains(MotivoParking.TipoNaoTratadoPrices.Name);
            }, TimeoutCurto));

            Assert.Single(motivosAcumulados);
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var profundidadePrincipal = await canalPublicador.MessageCountAsync(RabbitMqTopologia.FilaPrincipal);
        Assert.Equal(0u, profundidadePrincipal);
    }

    [Fact]
    public async Task PADROES_10_48_ExcecaoNaoClassificadaEstacionaComFalhaInesperada_ComControlePositivoDeTransitoriaQueRequeueia()
    {
        const string instrumentoId = "td:tesouro-teste-10-48-classificacao";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var clienteDeterministico = NovoId("cli-bug-deterministico");
        var tradeDeterministico = NovoId("trade-bug-deterministico");

        var clienteTransitorio = NovoId("cli-falha-transitoria");
        var tradeTransitorio = NovoId("trade-falha-transitoria");

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeDeterministico, clienteDeterministico, instrumentoId, 1m, 100m, dataEvento, registradoEm));
        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeTransitorio, clienteTransitorio, instrumentoId, 2m, 200m, dataEvento, registradoEm));

        var comportamento = new FalhaForcadaNoProcessamentoBehavior((evento, tentativa) =>
        {
            if (evento.TradeId == tradeDeterministico)
            {
                return new InvalidOperationException("bug determinístico plantado pelo teste — sempre lança");
            }

            if (evento.TradeId == tradeTransitorio && tentativa == 1)
            {
                return new NpgsqlException("falha de conexão com o Postgres simulada", new SocketException());
            }

            return null;
        });

        var (provider, consumidor, _) = CriarConsumidor(
            configurarServicosExtras: services => services.AddSingleton<
                IPipelineBehavior<ProcessarTradeRegisteredCommand, Result<ResultadoTradeRegistered>>>(comportamento));
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(
                await EsperarAsync(() => ExisteMovimentoAsync(tradeTransitorio), TimeoutCurto),
                "controle positivo (§10.8): a falha TRANSITÓRIA declarada tem que fazer nack com requeue e, na " +
                "redelivery seguinte — sem o gatilho mais presente —, o trade tem que ser escriturado normalmente; " +
                "prova de que a classificação realmente distingue e não estaciona tudo.");

            var motivosAcumulados = new HashSet<string>();
            Assert.True(await EsperarAsync(async () =>
            {
                foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                {
                    motivosAcumulados.Add(motivo);
                }

                return motivosAcumulados.Contains(MotivoParking.FalhaInesperadaNoProcessamento.Name);
            }, TimeoutCurto), "a exceção NÃO classificada tem que estacionar com o motivo falha_inesperada_no_processamento");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        Assert.False(
            await ExisteMovimentoAsync(tradeDeterministico),
            "a exceção determinística nunca pode terminar escriturando o movimento");

        var profundidadeDlq = await ContarMensagensAsync(RabbitMqTopologia.FilaDlq);
        Assert.Equal(0u, profundidadeDlq);

        var profundidadePrincipal = await canalPublicador.MessageCountAsync(RabbitMqTopologia.FilaPrincipal);
        Assert.True(
            profundidadePrincipal == 0u,
            "a exceção determinística tem que ser estacionada e retirada de circulação — nunca ficar " +
            "circulando em requeue infinito na fila principal.");
    }

    [Fact]
    public async Task PADROES_10_48_ResultFailureReconhecidoEDeterministico_EstacionaComFalhaInesperada_NuncaRequeueInfinito()
    {
        const string instrumentoId = "td:tesouro-teste-10-48-overflow-preco-medio";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var clienteId = NovoId("cli-overflow-preco-medio");
        var tradeQueEstoura = NovoId("trade-overflow-preco-medio");

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();

        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeQueEstoura, clienteId, instrumentoId, 1m, 5000000000000.00m, dataEvento, registradoEm));

        var (provider, consumidor, _) = CriarConsumidor();
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var motivosAcumulados = new HashSet<string>();
            Assert.True(
                await EsperarAsync(async () =>
                {
                    foreach (var motivo in await LerMotivosDaFilaParkedAsync())
                    {
                        motivosAcumulados.Add(motivo);
                    }

                    return motivosAcumulados.Contains(MotivoParking.FalhaInesperadaNoProcessamento.Name);
                }, TimeoutCurto),
                "quantidade=1 e valorFinanceiro=5e12 passam no CHECK de Movimento (numeric(18,2)/numeric(18,8)), " +
                "mas o preco_medio DERIVADO (valorFinanceiro / qtdDelta) estoura numeric(18,6) de posicao_corrente " +
                "— um Result.Failure RECONHECIDO (PostgresExceptionTranslator) e determinístico para este payload " +
                "chega ao handler; tem que estacionar, nunca ser tratado como transitório.");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        Assert.False(
            await ExisteMovimentoAsync(tradeQueEstoura),
            "a transação inteira tem que ter sido desfeita no overflow — nada gravado em movimentos");

        var profundidadePrincipal = await canalPublicador.MessageCountAsync(RabbitMqTopologia.FilaPrincipal);
        Assert.True(
            profundidadePrincipal == 0u,
            "o Result.Failure determinístico tem que ser estacionado e retirado de circulação — nunca ficar " +
            "circulando em requeue infinito na fila principal.");
    }

    [Fact]
    public async Task PADROES_10_48_PublisherConfirmNegadoAoEstacionar_NackComRequeueEDesfechoDeFalhaDeParkingNaoTransitorio()
    {
        var marcador = $"custodia-motivo-parking-teste-{Guid.NewGuid():N}";
        var routingKey = "prices." + marcador;

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, routingKey, "{}");

        var antesFalhaDeParking = LerContadorDesfecho("nack_requeue_falha_ao_estacionar");
        var antesTransitorio = LerContadorDesfecho("nack_requeue_transitorio");

        var (provider, consumidor, publicador) = CriarConsumidor(
            confirmar: (exchange, _) => exchange != RabbitMqTopologia.ExchangeParking);
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var duasNegativasDeParking = await EsperarAsync(
                () => Task.FromResult(publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeParking) >= 2),
                TimeoutCurto);

            Assert.True(
                duasNegativasDeParking,
                "com o publisher confirm sempre negado ao publicar em custodia.parking, a mensagem tem que ser " +
                "reentregue (nack com requeue) e reprocessada — é isso que produz a segunda tentativa de parking.");
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        var depoisFalhaDeParking = LerContadorDesfecho("nack_requeue_falha_ao_estacionar");
        var depoisTransitorio = LerContadorDesfecho("nack_requeue_transitorio");

        Assert.True(
            depoisFalhaDeParking > antesFalhaDeParking,
            "o publisher confirm negado AO ESTACIONAR não tem para onde estacionar — requeue é a única saída, " +
            "mas o desfecho tem que dizer que foi o PARKING que falhou, não 'transitório'.");
        Assert.True(
            antesTransitorio == depoisTransitorio,
            "o confirm negado ao estacionar NUNCA pode ser contabilizado como nack_requeue_transitorio — a " +
            "métrica mentiria sobre a causa.");

        Assert.DoesNotContain(MotivoParking.TipoNaoTratadoPrices.Name, await LerMotivosDaFilaParkedAsync());
    }

    [Fact]
    public async Task PADROES_10_48_ExcecaoNaoClassificadaFalhaTambemAoEstacionar_GuardaContraRecursaoTerminaEmFalhaDeParking()
    {
        const string instrumentoId = "td:tesouro-teste-10-48-guarda-recursao";
        var dataEvento = new DateOnly(2026, 6, 1);
        var registradoEm = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var clienteId = NovoId("cli-guarda-recursao");
        var tradeId = NovoId("trade-guarda-recursao-bug-deterministico");

        await using var conexaoPublicadora = await fixture.CriarConexaoAmqpAsync();
        await using var canalPublicador = await conexaoPublicadora.CreateChannelAsync();
        await PublicarAsync(canalPublicador, "trades.registered", TradePayloadBuilder.Aplicacao(
            tradeId, clienteId, instrumentoId, 1m, 100m, dataEvento, registradoEm));

        var comportamento = new FalhaForcadaNoProcessamentoBehavior((evento, _) =>
            evento.TradeId == tradeId
                ? new InvalidOperationException("bug determinístico plantado pelo teste — sempre lança")
                : null);

        var antesFalhaDeParking = LerContadorDesfecho("nack_requeue_falha_ao_estacionar");

        var (provider, consumidor, publicador) = CriarConsumidor(
            lancarExcecaoAoPublicar: (exchange, _) =>
                exchange == RabbitMqTopologia.ExchangeParking ? new SocketException() : null,
            configurarServicosExtras: services => services.AddSingleton<
                IPipelineBehavior<ProcessarTradeRegisteredCommand, Result<ResultadoTradeRegistered>>>(comportamento));
        await using var _ = provider;
        await consumidor.StartAsync(CancellationToken.None);
        try
        {
            var guardaTerminouEmFalhaDeParking = await EsperarAsync(
                () => Task.FromResult(LerContadorDesfecho("nack_requeue_falha_ao_estacionar") > antesFalhaDeParking),
                TimeoutCurto);

            Assert.True(
                guardaTerminouEmFalhaDeParking,
                "a exceção não classificada tenta estacionar; se ESSA tentativa também lançar, a guarda contra " +
                "recursão tem que terminar em nack+requeue com o desfecho de falha de parking — sem chamar " +
                "EstacionarAsync de novo dentro do mesmo processamento, e sem travar o teste.");

            var tentativasDeParkingObservadas = publicador.Chamadas.Count(c => c.Exchange == RabbitMqTopologia.ExchangeParking);
            Assert.InRange(tentativasDeParkingObservadas, 1, 500);
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }

        Assert.False(await ExisteMovimentoAsync(tradeId));
        Assert.DoesNotContain(MotivoParking.FalhaInesperadaNoProcessamento.Name, await LerMotivosDaFilaParkedAsync());
    }

    private async Task VarrerBijecaoAsync(IReadOnlyList<(string ClienteId, string TradeId)> esperados)
    {
        await using var db = fixture.CriarDbContext();
        var clientesEsperados = esperados.Select(e => e.ClienteId).ToHashSet();
        var linhas = await db.Movimentos.Where(m => clientesEsperados.Contains(m.ClienteId)).ToListAsync();

        foreach (var (_, tradeId) in esperados)
        {
            Assert.Contains(linhas, l => l.RefExterna == tradeId);
        }

        Assert.Equal(esperados.Count, linhas.Count);
    }

    private (ServiceProvider Provider, RabbitMqTradeConsumidor Consumidor, PublicadorComFalhaForcada Publicador) CriarConsumidor(
        Func<string, int, bool>? confirmar = null,
        Func<string, int, bool>? entregaMesmoComConfirmNegado = null,
        IDictionary<string, string?>? configExtras = null,
        Action<IServiceCollection>? configurarServicosExtras = null,
        Func<string, int, Exception?>? lancarExcecaoAoPublicar = null)
    {
        var configuration = fixture.CriarConfiguration(configExtras);
        var provider = fixture.CriarServiceProvider(configuration, configurarServicosExtras);
        var connectionProvider = provider.GetRequiredService<RabbitMqConnectionProvider>();
        var publicadorReal = provider.GetRequiredService<IPublicadorComConfirmacao>();
        var publicador = new PublicadorComFalhaForcada(
            publicadorReal, confirmar, entregaMesmoComConfirmNegado, lancarExcecaoAoPublicar);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var roteador = provider.GetRequiredService<Custodia.Application.Eventos.RoteadorDeEventos>();
        var metrics = provider.GetRequiredService<ConsumidorMetrics>();
        var logger = provider.GetRequiredService<ILogger<RabbitMqTradeConsumidor>>();

        var consumidor = new RabbitMqTradeConsumidor(
            connectionProvider, publicador, scopeFactory, roteador, metrics, configuration, logger);

        return (provider, consumidor, publicador);
    }

    private async Task<bool> ExisteMovimentoAsync(string refExterna)
    {
        await using var db = fixture.CriarDbContext();
        return await db.Movimentos.AnyAsync(m => m.RefExterna == refExterna);
    }

    private async Task<uint> ContarMensagensAsync(string fila)
    {
        await using var conexao = await fixture.CriarConexaoAmqpAsync();
        await using var canal = await conexao.CreateChannelAsync();
        return await canal.MessageCountAsync(fila);
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

    private static async Task PublicarAsync(
        IChannel canal, string routingKey, string corpo, IDictionary<string, object?>? headers = null)
    {
        var propriedades = new BasicProperties();
        if (headers is not null)
        {
            propriedades.Headers = headers;
        }

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

    private static string NovoId(string prefixo) => $"{prefixo}-{Guid.NewGuid():N}";
}

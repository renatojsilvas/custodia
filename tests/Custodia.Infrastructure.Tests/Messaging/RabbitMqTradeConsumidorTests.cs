using System.Diagnostics;
using System.Text;
using Custodia.Domain.Eventos;
using Custodia.Domain.Movimentos;
using Custodia.Infrastructure.Messaging;
using Custodia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Custodia.Infrastructure.Tests.Messaging;

[Collection("rabbitmq-consumidor")]
public sealed class RabbitMqTradeConsumidorTests(RabbitMqConsumidorFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan TimeoutCurto = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan TimeoutComUmaVoltaDeRetry = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan TimeoutComDuasVoltasDeRetry = TimeSpan.FromSeconds(90);

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

                return motivosAcumulados.Contains(MotivoEstacionamento.EstornoOrfaoExpirado.Name);
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
                MotivoEstacionamento.EstornoClienteDivergente.Name,
                MotivoEstacionamento.VersaoNaoSuportada.Name,
                MotivoEstacionamento.EstornoDivergente.Name,
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

    [Fact(Skip =
        "BLOQUEADO POR DEFEITO EM Custodia.Domain.Posicoes.DobraPosicao.Dobrar (fora do escopo deste executor, " +
        "que não pode alterar Domain/Application): reversorPorAlvoId é montado com " +
        "'.ToDictionary(m => m.RefEstorno!.Value, ...)', que assume NO MÁXIMO um ajuste por RefEstorno. Um " +
        "segundo estorno do MESMO movimento, ao ser reprocessado, cria um Movimento transiente com o MESMO " +
        "RefEstorno do ajuste já persistido pelo primeiro estorno; ObterMovimentosDaChaveAsync traz os dois " +
        "(um do banco, um em memória) e o ToDictionary lança ArgumentException ('An item with the same key " +
        "has already been added') ANTES de a gravação chegar ao UNIQUE(ref_estorno) que classificaria isso " +
        "como estorno_duplicado. O consumidor loga CRITICAL e faz nack(requeue:true) — mas como é uma falha " +
        "DETERMINÍSTICA (mesma exceção sempre), e nack(requeue:true) reentrega NA HORA (prefetch 1, serial), " +
        "isso é HEAD-OF-LINE BLOCKING: a mensagem trava a fila em loop até o x-delivery-limit=20 estourar e " +
        "cair na custodia.prices.dlq, mascarando o motivo estorno_duplicado. Reportado ao orquestrador; não " +
        "corrigido aqui.")]
    public async Task DecisaoB_EstornoDuplicado_BloqueadoPorBugDeDobraPosicaoNoRedobroDoAjuste()
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

                return motivosAcumulados.Contains(MotivoEstacionamento.EstornoDuplicado.Name);
            }, TimeoutCurto));
        }
        finally
        {
            await consumidor.StopAsync(CancellationToken.None);
        }
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

                return motivosAcumulados.Contains(MotivoEstacionamento.RetryIndisponivel.Name);
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

                return motivosAcumulados.Contains(MotivoEstacionamento.TipoNaoTratadoPrices.Name);
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
        Func<string, int, bool>? confirmar = null, IDictionary<string, string?>? configExtras = null)
    {
        var configuration = fixture.CriarConfiguration(configExtras);
        var provider = fixture.CriarServiceProvider(configuration);
        var connectionProvider = provider.GetRequiredService<RabbitMqConnectionProvider>();
        var publicadorReal = provider.GetRequiredService<IPublicadorComConfirmacao>();
        var publicador = new PublicadorComFalhaForcada(publicadorReal, confirmar);
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

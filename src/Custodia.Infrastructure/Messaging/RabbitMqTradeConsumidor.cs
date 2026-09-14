using System.Globalization;
using System.Text;
using System.Text.Json;
using Custodia.Application.Eventos;
using Custodia.Application.Precos;
using Custodia.Domain.Eventos;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Custodia.Infrastructure.Messaging;

public sealed class RabbitMqTradeConsumidor : BackgroundService
{
    private const long OrfaoTetoVoltasPadrao = 10;
    private const long RetryTetoTentativasPadrao = 5;
    private const ushort ReplyCodePreconditionFailed = 406;
    private static readonly TimeSpan AtrasoDeReconexao = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IPublicadorComConfirmacao _publicador;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RoteadorDeEventos _roteador;
    private readonly ConsumidorMetrics _metrics;
    private readonly ILogger<RabbitMqTradeConsumidor> _logger;
    private readonly long _orfaoTetoVoltas;
    private readonly long _retryTetoTentativas;
    private IChannel? _canal;

    public RabbitMqTradeConsumidor(
        RabbitMqConnectionProvider connectionProvider,
        IPublicadorComConfirmacao publicador,
        IServiceScopeFactory scopeFactory,
        RoteadorDeEventos roteador,
        ConsumidorMetrics metrics,
        IConfiguration configuration,
        ILogger<RabbitMqTradeConsumidor> logger)
    {
        _connectionProvider = connectionProvider;
        _publicador = publicador;
        _scopeFactory = scopeFactory;
        _roteador = roteador;
        _metrics = metrics;
        _logger = logger;
        _orfaoTetoVoltas = LerInteiroOuPadrao(configuration["Decisao:OrfaoTetoVoltas"], OrfaoTetoVoltasPadrao);
        _retryTetoTentativas = LerInteiroOuPadrao(configuration["Decisao:RetryTetoTentativas"], RetryTetoTentativasPadrao);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConectarEConsumirAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == ReplyCodePreconditionFailed)
            {
                _logger.LogCritical(
                    ex,
                    "Topologia da {Fila} divergente da declarada nesta versão (406 PRECONDITION_FAILED). " +
                    "Falha alta e desejável: não será repetida.",
                    RabbitMqTopologia.FilaPrincipal);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao conectar ao RabbitMQ ou assinar a {Fila}; nova tentativa em {Atraso}.",
                    RabbitMqTopologia.FilaPrincipal, AtrasoDeReconexao);
                await Task.Delay(AtrasoDeReconexao, stoppingToken);
            }
        }
    }

    private async Task ConectarEConsumirAsync(CancellationToken stoppingToken)
    {
        var conexao = await _connectionProvider.ObterConexaoAsync(stoppingToken);
        _canal = await conexao.CreateChannelAsync(cancellationToken: stoppingToken);

        await _canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += ProcessarAsync;

        await _canal.BasicConsumeAsync(
            queue: RabbitMqTopologia.FilaPrincipal,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumidor,
            cancellationToken: stoppingToken);

        await AguardarParadaAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_canal is not null)
        {
            await _canal.DisposeAsync();
            _canal = null;
        }
    }

    private static async Task AguardarParadaAsync(CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource();
        await using (stoppingToken.Register(() => tcs.TrySetResult()))
        {
            await tcs.Task;
        }
    }

    private async Task ProcessarAsync(object sender, BasicDeliverEventArgs ea)
    {
        using var duracao = _metrics.MedirDuracao();

        try
        {
            await ProcessarEntregaAsync(ea);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ClassificadorDeFalhaTransitoria.EhTransitoria(ex))
        {
            _logger.LogError(
                ex,
                "Falha transitória conhecida ao processar mensagem da {Fila} (routingKey {RoutingKey}); nack com requeue.",
                RabbitMqTopologia.FilaPrincipal, ea.RoutingKey);
            await NackRequeueAsync(ea);
            _metrics.RegistrarDesfecho("nack_requeue_transitorio");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await TratarFalhaNaoClassificadaAsync(ea, ex);
        }
    }

    private async Task TratarFalhaNaoClassificadaAsync(BasicDeliverEventArgs ea, Exception excecaoNaoClassificada)
    {
        _logger.LogCritical(
            excecaoNaoClassificada,
            "Falha não classificada ao processar mensagem da {Fila} (routingKey {RoutingKey}); estacionando com motivo " +
            "{Motivo} em vez de requeue indefinido.",
            RabbitMqTopologia.FilaPrincipal, ea.RoutingKey, MotivoParking.FalhaInesperadaNoProcessamento.Name);

        var corpo = ea.Body.ToArray();
        var cabecalhosOriginais = RabbitMqCabecalhos.Copiar(ea.BasicProperties.Headers);

        try
        {
            await EstacionarAsync(ea, cabecalhosOriginais, MotivoParking.FalhaInesperadaNoProcessamento, corpo);
        }
        catch (Exception excecaoAoEstacionar) when (excecaoAoEstacionar is not OperationCanceledException)
        {
            _logger.LogCritical(
                excecaoAoEstacionar,
                "Falha ao tentar estacionar mensagem da {Fila} (routingKey {RoutingKey}) após falha não classificada; " +
                "nack com requeue.",
                RabbitMqTopologia.FilaPrincipal, ea.RoutingKey);
            await NackRequeueComFalhaDeParkingAsync(ea);
        }
    }

    private async Task ProcessarEntregaAsync(BasicDeliverEventArgs ea)
    {
        var corpo = ea.Body.ToArray();
        var corpoTexto = Encoding.UTF8.GetString(corpo);
        var cabecalhosOriginais = RabbitMqCabecalhos.Copiar(ea.BasicProperties.Headers);

        DesfechoRoteamento desfecho;
        try
        {
            desfecho = _roteador.Rotear(ea.RoutingKey, corpoTexto);
        }
        catch (JsonException)
        {
            desfecho = DesfechoRoteamento.Estacionar(MotivoParking.PayloadInvalido);
        }

        switch (desfecho.Tipo)
        {
            case DesfechoRoteamentoTipo.Ignorar:
                await AckAsync(ea);
                _metrics.RegistrarDesfecho("ack_ignorado_sonda");
                return;

            case DesfechoRoteamentoTipo.Estacionar:
                await EstacionarAsync(ea, cabecalhosOriginais, desfecho.Motivo!, corpo);
                return;

            case DesfechoRoteamentoTipo.Escriturar:
                await ProcessarTradeAsync(ea, cabecalhosOriginais, desfecho.Evento!, corpo);
                return;

            case DesfechoRoteamentoTipo.Observar:
                await ProcessarPrecoAsync(ea, cabecalhosOriginais, desfecho.EventoPreco!, corpo);
                return;

            default:
                throw new InvalidOperationException($"Desfecho de roteamento não reconhecido: '{desfecho.Tipo}'.");
        }
    }

    private async Task ProcessarTradeAsync(
        BasicDeliverEventArgs ea, Dictionary<string, object?> cabecalhosOriginais, TradeRegisteredEvento evento, byte[] corpo)
    {
        using var escopo = _scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
        var resultado = await mediator.Send(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        if (resultado.IsFailure)
        {
            _logger.LogCritical(
                "Falha reconhecida e determinística ao processar TradeRegistered {TradeId} (cliente {ClienteId}): " +
                "{Codigo} - {Mensagem}. Estacionando com motivo {Motivo} em vez de requeue indefinido.",
                evento.TradeId, evento.ClienteId, resultado.Error.Code, resultado.Error.Description,
                MotivoParking.FalhaInesperadaNoProcessamento.Name);
            await EstacionarAsync(ea, cabecalhosOriginais, MotivoParking.FalhaInesperadaNoProcessamento, corpo);
            return;
        }

        switch (resultado.Value.Tipo)
        {
            case ResultadoTradeRegisteredTipo.Escriturado:
                await AckAsync(ea);
                _metrics.RegistrarDesfecho(resultado.Value.Replay ? "ack_replay" : "ack_escriturado");
                return;

            case ResultadoTradeRegisteredTipo.Estacionar:
                await EstacionarAsync(ea, cabecalhosOriginais, resultado.Value.Motivo!, corpo);
                return;

            case ResultadoTradeRegisteredTipo.EnviarParaRetry:
                await ProcessarOrfaoAsync(ea, cabecalhosOriginais, corpo);
                return;

            default:
                throw new InvalidOperationException(
                    $"Resultado de TradeRegistered não reconhecido: '{resultado.Value.Tipo}'.");
        }
    }

    private async Task ProcessarPrecoAsync(
        BasicDeliverEventArgs ea, Dictionary<string, object?> cabecalhosOriginais, PriceObservedEvento evento, byte[] corpo)
    {
        using var escopo = _scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
        var resultado = await mediator.Send(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        if (resultado.IsFailure)
        {
            _logger.LogCritical(
                "Falha reconhecida e determinística ao processar PriceObserved (instrumento {InstrumentoId}, campo " +
                "{Campo}, data {DataRef}, revisão {Revisao}): {Codigo} - {Mensagem}. Estacionando com motivo {Motivo} " +
                "em vez de requeue indefinido.",
                evento.InstrumentoId, evento.Campo, evento.DataRef, evento.Revisao, resultado.Error.Code,
                resultado.Error.Description, MotivoParking.FalhaInesperadaNoProcessamento.Name);
            await EstacionarAsync(ea, cabecalhosOriginais, MotivoParking.FalhaInesperadaNoProcessamento, corpo);
            return;
        }

        switch (resultado.Value.Tipo)
        {
            case ResultadoPriceObservedTipo.AplicadoEmPrecoAtual:
                await AckAsync(ea);
                _metrics.RegistrarDesfecho("ack_preco_aplicado_preco_atual");
                return;

            case ResultadoPriceObservedTipo.SoHistorico:
                await AckAsync(ea);
                _metrics.RegistrarDesfecho("ack_preco_so_historico");
                return;

            case ResultadoPriceObservedTipo.ReplaySemAlteracao:
                await AckAsync(ea);
                _metrics.RegistrarDesfecho("ack_preco_replay");
                return;

            case ResultadoPriceObservedTipo.ValorDivergente:
                await AckAsync(ea);
                _metrics.RegistrarDesfecho("ack_preco_valor_divergente");
                return;

            default:
                throw new InvalidOperationException(
                    $"Resultado de PriceObserved não reconhecido: '{resultado.Value.Tipo}'.");
        }
    }

    private async Task ProcessarOrfaoAsync(BasicDeliverEventArgs ea, Dictionary<string, object?> cabecalhosOriginais, byte[] corpo)
    {
        var voltasJaFeitas = RabbitMqCabecalhos.LerVoltas(cabecalhosOriginais);

        if (voltasJaFeitas >= _orfaoTetoVoltas)
        {
            await EstacionarAsync(ea, cabecalhosOriginais, MotivoParking.EstornoOrfaoExpirado, corpo);
            return;
        }

        var tentativasDeEntregaNestaFila = RabbitMqCabecalhos.ContarTentativasDeEntrega(cabecalhosOriginais);

        if (tentativasDeEntregaNestaFila >= _retryTetoTentativas)
        {
            await EstacionarAsync(ea, cabecalhosOriginais, MotivoParking.RetryIndisponivel, corpo);
            return;
        }

        var cabecalhosParaRepublicar = new Dictionary<string, object?>(cabecalhosOriginais);
        RabbitMqCabecalhos.IncrementarVoltas(cabecalhosParaRepublicar);

        var confirmado = await _publicador.PublicarAsync(
            RabbitMqTopologia.ExchangeRetryIn, ea.RoutingKey, cabecalhosParaRepublicar, corpo, CancellationToken.None);

        if (!confirmado)
        {
            _logger.LogWarning(
                "Publisher confirm negado ao republicar estorno órfão em {Exchange} (tentativa {Tentativa} nesta " +
                "entrega); custodia.retry indisponível, nack com requeue — limitado pelo teto de tentativas.",
                RabbitMqTopologia.ExchangeRetryIn, tentativasDeEntregaNestaFila + 1);
            await NackRequeueAsync(ea);
            _metrics.RegistrarDesfecho("nack_requeue_falha_ao_publicar_retry");
            return;
        }

        await AckAsync(ea);
        _metrics.RegistrarDesfecho("retry_publicado");
    }

    private async Task EstacionarAsync(
        BasicDeliverEventArgs ea, Dictionary<string, object?> cabecalhosOriginais, MotivoParking motivo, byte[] corpo)
    {
        var cabecalhosParaRepublicar = new Dictionary<string, object?>(cabecalhosOriginais);
        RabbitMqCabecalhos.DefinirMotivo(cabecalhosParaRepublicar, motivo.Name);

        var confirmado = await _publicador.PublicarAsync(
            RabbitMqTopologia.ExchangeParking, ea.RoutingKey, cabecalhosParaRepublicar, corpo, CancellationToken.None);

        if (!confirmado)
        {
            _logger.LogCritical(
                "Publisher confirm negado ao republicar mensagem com motivo {Motivo} em {Exchange}; parking " +
                "indisponível, nack com requeue.",
                motivo.Name, RabbitMqTopologia.ExchangeParking);
            await NackRequeueComFalhaDeParkingAsync(ea);
            return;
        }

        await AckAsync(ea);
        _metrics.RegistrarDesfecho("park_publicado");
        _metrics.RegistrarEstacionamento(motivo.Name);
    }

    private async Task NackRequeueComFalhaDeParkingAsync(BasicDeliverEventArgs ea)
    {
        await NackRequeueAsync(ea);
        _metrics.RegistrarDesfecho("nack_requeue_falha_ao_estacionar");
    }

    private ValueTask AckAsync(BasicDeliverEventArgs ea) =>
        _canal!.BasicAckAsync(ea.DeliveryTag, multiple: false, CancellationToken.None);

    private ValueTask NackRequeueAsync(BasicDeliverEventArgs ea) =>
        _canal!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);

    private static long LerInteiroOuPadrao(string? valor, long padrao)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return padrao;
        }

        return long.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resultado)
            ? resultado
            : padrao;
    }
}

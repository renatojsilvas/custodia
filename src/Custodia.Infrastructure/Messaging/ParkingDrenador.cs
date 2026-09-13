using System.Globalization;
using System.Text;
using Custodia.Application.Eventos;
using Custodia.Domain.Eventos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Custodia.Infrastructure.Messaging;

public sealed class ParkingDrenador
{
    private const long TetoPadrao = 10_000;

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IPublicadorComConfirmacao _publicador;
    private readonly IMensagemParkingReprocessador _reprocessador;
    private readonly IIdentificadorDePassagem _identificadorDePassagem;
    private readonly IPontoDeSuspensaoDrenagem _pontoDeSuspensao;
    private readonly ParkingDrenadorMetrics _metrics;
    private readonly ILogger<ParkingDrenador> _logger;
    private readonly long _teto;

    public ParkingDrenador(
        RabbitMqConnectionProvider connectionProvider,
        IPublicadorComConfirmacao publicador,
        IMensagemParkingReprocessador reprocessador,
        IIdentificadorDePassagem identificadorDePassagem,
        IPontoDeSuspensaoDrenagem pontoDeSuspensao,
        ParkingDrenadorMetrics metrics,
        IConfiguration configuration,
        ILogger<ParkingDrenador> logger)
    {
        _connectionProvider = connectionProvider;
        _publicador = publicador;
        _reprocessador = reprocessador;
        _identificadorDePassagem = identificadorDePassagem;
        _pontoDeSuspensao = pontoDeSuspensao;
        _metrics = metrics;
        _logger = logger;
        _teto = LerInteiroOuPadrao(configuration["Decisao:ParkingDrenagemTeto"], TetoPadrao);
    }

    public async Task<ResultadoDrenagem> DrenarAsync(
        MotivoParking motivo, string? passagemIdForcado, CancellationToken ct)
    {
        var conexao = await _connectionProvider.ObterConexaoAsync(ct);
        await using var canal = await conexao.CreateChannelAsync(cancellationToken: ct);

        var estoque = (long)await canal.MessageCountAsync(RabbitMqTopologia.FilaParked, ct);

        await _pontoDeSuspensao.AposLerEstoqueAsync(ct);

        if (estoque > _teto)
        {
            _logger.LogError(
                "Passagem de drenagem da custodia.parked abortada: estoque {Estoque} acima do teto {Teto}.",
                estoque, _teto);
            return new ResultadoDrenagem(DesfechoDrenagem.LimitePorTeto, motivo.Name, string.Empty, estoque, _teto, 0, 0);
        }

        var passagemId = passagemIdForcado ?? _identificadorDePassagem.GerarNovoId();

        long examinadas = 0;
        long nMotivo = 0;
        long residualMotivo = 0;
        var voltaDetectada = false;
        var interrompidaPorFalhaDeInfra = false;

        while (examinadas < estoque)
        {
            BasicGetResult? entrega;

            try
            {
                entrega = await canal.BasicGetAsync(RabbitMqTopologia.FilaParked, autoAck: false, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex, "Falha ao consumir da custodia.parked durante a passagem {PassagemId}.", passagemId);
                interrompidaPorFalhaDeInfra = true;
                break;
            }

            if (entrega is null)
            {
                break;
            }

            examinadas++;

            var cabecalhos = RabbitMqCabecalhos.Copiar(entrega.BasicProperties.Headers);
            var corpo = entrega.Body.ToArray();

            if (RabbitMqCabecalhos.LerPassagemId(cabecalhos) == passagemId)
            {
                voltaDetectada = true;
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, ct);
                break;
            }

            if (EhSondaDeDeployF2(corpo))
            {
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
                continue;
            }

            var motivoDaMensagem = RabbitMqCabecalhos.LerMotivo(cabecalhos);

            if (!string.Equals(motivoDaMensagem, motivo.Name, StringComparison.Ordinal))
            {
                if (!await RepublicarAsync(canal, entrega, cabecalhos, corpo, passagemId, ct))
                {
                    interrompidaPorFalhaDeInfra = true;
                    break;
                }

                continue;
            }

            var resultadoReprocessamento = await _reprocessador.ReprocessarAsync(entrega.RoutingKey, corpo, motivo, ct);

            if (resultadoReprocessamento.Tipo is ResultadoReprocessamentoTipo.Processado or ResultadoReprocessamentoTipo.Ignorado)
            {
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
                nMotivo++;
                continue;
            }

            RabbitMqCabecalhos.DefinirMotivo(cabecalhos, resultadoReprocessamento.MotivoResultante!.Name);

            if (!await RepublicarAsync(canal, entrega, cabecalhos, corpo, passagemId, ct))
            {
                interrompidaPorFalhaDeInfra = true;
                break;
            }

            residualMotivo++;
        }

        var desfecho = Classificar(voltaDetectada, interrompidaPorFalhaDeInfra, examinadas, estoque, residualMotivo, nMotivo);

        if (desfecho is DesfechoDrenagem.Completude or DesfechoDrenagem.Parcial or DesfechoDrenagem.VazioDoMotivo)
        {
            _metrics.RegistrarResidualPorMotivo(motivo.Name, residualMotivo);
        }

        return new ResultadoDrenagem(desfecho, motivo.Name, passagemId, estoque, _teto, nMotivo, residualMotivo);
    }

    private static DesfechoDrenagem Classificar(
        bool voltaDetectada, bool interrompidaPorFalhaDeInfra, long examinadas, long estoque, long residualMotivo, long nMotivo)
    {
        if (voltaDetectada)
        {
            return DesfechoDrenagem.LimitePorVolta;
        }

        if (interrompidaPorFalhaDeInfra || examinadas < estoque)
        {
            return DesfechoDrenagem.Interrompida;
        }

        if (residualMotivo > 0)
        {
            return DesfechoDrenagem.Parcial;
        }

        return nMotivo == 0 ? DesfechoDrenagem.VazioDoMotivo : DesfechoDrenagem.Completude;
    }

    private async Task<bool> RepublicarAsync(
        IChannel canal,
        BasicGetResult entrega,
        Dictionary<string, object?> cabecalhos,
        byte[] corpo,
        string passagemId,
        CancellationToken ct)
    {
        RabbitMqCabecalhos.DefinirPassagemId(cabecalhos, passagemId);

        var confirmado = await _publicador.PublicarAsync(
            RabbitMqTopologia.ExchangeParking, entrega.RoutingKey, cabecalhos, corpo, ct);

        if (!confirmado)
        {
            _logger.LogCritical(
                "Publisher confirm negado ao republicar na custodia.parking durante a passagem {PassagemId}.",
                passagemId);
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, ct);
            return false;
        }

        await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
        return true;
    }

    private static bool EhSondaDeDeployF2(byte[] corpo)
    {
        var texto = Encoding.UTF8.GetString(corpo);
        return !EhJson(texto) && texto.StartsWith(RoteadorDeEventos.PrefixoSondaDeDeploy, StringComparison.Ordinal);
    }

    private static bool EhJson(string texto)
    {
        var aparado = texto.AsSpan().TrimStart();
        return aparado.Length > 0 && aparado[0] == '{';
    }

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

using Prometheus;

namespace Custodia.Infrastructure.Messaging;

public sealed class ConsumidorMetrics
{
    private static readonly Counter MensagensPorDesfechoTotal = Metrics.CreateCounter(
        "custodia_consumo_mensagens_total",
        "Total de mensagens da custodia.prices processadas pelo consumidor, por desfecho " +
        "(ack_escriturado|ack_replay|ack_ignorado_sonda|nack_requeue_transitorio|retry_publicado|park_publicado).",
        new CounterConfiguration
        {
            LabelNames = ["desfecho"],
        });

    private static readonly Counter EstacionamentosPorMotivoTotal = Metrics.CreateCounter(
        "custodia_estacionamento_mensagens_total",
        "Total de mensagens republicadas na custodia.parked pelo consumidor, por motivo " +
        "(x-custodia-motivo). Cardinalidade fechada: os motivos são a lista de MotivoParking.",
        new CounterConfiguration
        {
            LabelNames = ["motivo"],
        });

    private static readonly Histogram DuracaoProcessamentoSegundos = Metrics.CreateHistogram(
        "custodia_consumo_duracao_segundos",
        "Duração do processamento de uma mensagem da custodia.prices, do recebimento ao ack/nack.");

    private static readonly Gauge FilaProfundidade = Metrics.CreateGauge(
        "custodia_fila_profundidade",
        "Quantidade de mensagens prontas (messages_ready) nas filas da custódia, medida periodicamente " +
        "via AMQP (queue.declare passivo), por fila.",
        new GaugeConfiguration
        {
            LabelNames = ["fila"],
        });

    public void RegistrarDesfecho(string desfecho) => MensagensPorDesfechoTotal.WithLabels(desfecho).Inc();

    public void RegistrarEstacionamento(string motivo) => EstacionamentosPorMotivoTotal.WithLabels(motivo).Inc();

    public IDisposable MedirDuracao() => DuracaoProcessamentoSegundos.NewTimer();

    public void RegistrarProfundidadeDaFila(string fila, long quantidadeDeMensagens) =>
        FilaProfundidade.WithLabels(fila).Set(quantidadeDeMensagens);
}

using Prometheus;

namespace Custodia.Infrastructure.Messaging;

public sealed class ParkingDrenadorMetrics
{
    private static readonly Gauge ResidualPorMotivo = Metrics.CreateGauge(
        "custodia_parked_mensagens",
        "Mensagens do motivo ainda estacionadas na custodia.parked ao fim de uma passagem completa do " +
        "drenador (residual_motivo). Publicada só quando a passagem examinou as N do estoque " +
        "(COMPLETUDE, PARCIAL, VAZIO_DO_MOTIVO); nunca um gauge de processo, sempre o resultado da varredura.",
        new GaugeConfiguration
        {
            LabelNames = ["motivo"],
        });

    public void RegistrarResidualPorMotivo(string motivo, long residualMotivo) =>
        ResidualPorMotivo.WithLabels(motivo).Set(residualMotivo);

    public double LerResidualPorMotivo(string motivo) => ResidualPorMotivo.WithLabels(motivo).Value;
}

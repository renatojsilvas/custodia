using Custodia.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Custodia.Infrastructure.Observability;

public sealed class BusinessMetrics(ILogger<BusinessMetrics> logger) : IBusinessMetrics
{
    private static readonly Counter PosicaoNegativaSinalizadaTotal = Metrics.CreateCounter(
        "custodia_posicao_negativa_sinalizada_total",
        "Total de vezes em que a Custódia sinalizou uma posição corrente negativa após aplicar um movimento " +
        "(camada 3 da validação em três camadas — ADR-11). Rótulo por instrumento, nunca por cliente " +
        "(cardinalidade ilimitada).",
        new CounterConfiguration
        {
            LabelNames = ["instrumento_id"]
        });

    public void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante)
    {
        PosicaoNegativaSinalizadaTotal.WithLabels(instrumentoId).Inc();

        logger.LogWarning(
            "Posição negativa sinalizada: cliente {ClienteId}, instrumento {InstrumentoId}, quantidade resultante {QuantidadeResultante}. " +
            "Correção esperada por estorno.",
            clienteId,
            instrumentoId,
            quantidadeResultante);
    }
}

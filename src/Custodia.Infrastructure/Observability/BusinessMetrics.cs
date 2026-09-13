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

    private static readonly Gauge CalendarioDiasUteisHorizonteDiasRestantesGauge = Metrics.CreateGauge(
        "custodia_calendario_dias_uteis_horizonte_dias_restantes",
        "Dias corridos entre hoje (America/Sao_Paulo) e o último dia presente em calendario_dias_uteis. " +
        "Guarda contra o horizonte do seed se esgotar sem aviso.");

    private static readonly Counter ResgateTributadoSobrePrecoMedioProvisorioTotal = Metrics.CreateCounter(
        "custodia_resgate_tributado_sobre_preco_medio_provisorio_total",
        "Total de resgates cuja fila de lotes FIFO não cobriu a quantidade resgatada — o tributo foi " +
        "calculado sobre o custo e a data do último lote da fila (ou zero/hoje com a fila vazia). " +
        "Distinto da posição negativa sinalizada: aqui já existe linha de tributo gravada no livro. " +
        "Conserto esperado por estorno e relançamento após a compra faltante entrar.",
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

    public void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados)
    {
        CalendarioDiasUteisHorizonteDiasRestantesGauge.Set(diasRestantes);

        if (diasRestantes < diasMinimosConfigurados)
        {
            logger.LogWarning(
                "Horizonte do calendário de dias úteis abaixo do mínimo configurado: restam {DiasRestantes} dias, " +
                "mínimo configurado é {DiasMinimosConfigurados}. Semeie mais datas em calendario_dias_uteis por migration.",
                diasRestantes,
                diasMinimosConfigurados);
        }
    }

    public void RegistrarResgateTributadoSobrePrecoMedioProvisorio(
        string clienteId, string instrumentoId, decimal quantidadeDescoberta)
    {
        ResgateTributadoSobrePrecoMedioProvisorioTotal.WithLabels(instrumentoId).Inc();

        logger.LogWarning(
            "Resgate tributado sobre preço médio provisório: cliente {ClienteId}, instrumento {InstrumentoId}, " +
            "quantidade descoberta pela fila de lotes {QuantidadeDescoberta}. Correção esperada por estorno e " +
            "relançamento após a compra faltante entrar.",
            clienteId,
            instrumentoId,
            quantidadeDescoberta);
    }
}

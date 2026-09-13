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

    private static readonly Counter LiquidacaoLimitePorTetoTotal = Metrics.CreateCounter(
        "custodia_liquidacao_limite_por_teto_total",
        "Total de ciclos do job de liquidação abortados por excederem o teto de candidatas por ciclo " +
        "(PADROES 10.31 — parada por LIMITE, nunca sucesso parcial).");

    private static readonly Counter LiquidacaoCalendarioExauridoTotal = Metrics.CreateCounter(
        "custodia_liquidacao_calendario_exaurido_total",
        "Total de vezes em que o job de liquidação encontrou o horizonte do calendário de dias úteis esgotado " +
        "ao tentar calcular D+1 útil de uma linha a_liquidar vencida. Falha alta, nunca modo degradado.");

    private static readonly Counter LiquidacaoCandidataInconsistenteTotal = Metrics.CreateCounter(
        "custodia_liquidacao_candidata_inconsistente_total",
        "Total de candidatas de liquidação puladas por inconsistência (a_liquidar sem o movimento principal " +
        "correspondente) — estado impossível por construção em produção, mas que o job não pode assumir. " +
        "O ciclo continua para as demais candidatas; a linha órfã realerta a cada ciclo até correção manual.");

    private static readonly Gauge GuardaResgatesSemAliqGauge = Metrics.CreateGauge(
        "custodia_guarda_resgates_sem_aliq",
        "Guarda permanente 1/4 do F5: quantidade de resgates efetivos (venda não revertida) sem a linha " +
        "aliq:<tradeId> — discriminador incondicional, gravado com ou sem tributo porque a_liquidar entra " +
        "BRUTO. Deveria ser sempre zero fora da janela F4→F5; reaparecer indica bug do handler ou backfill " +
        "pendente.");

    private static readonly Gauge GuardaAjustesDeResgateSemReversaoGauge = Metrics.CreateGauge(
        "custodia_guarda_ajustes_de_resgate_sem_reversao",
        "Guarda permanente 2/4 do F5: quantidade de ajustes sobre resgate cujo conjunto est: correspondente " +
        "ainda não foi gravado por inteiro (discriminado por est:aliq:, o mesmo padrão da guarda 1). Deveria " +
        "ser sempre zero fora da janela F4→F5.");

    private static readonly Gauge GuardaALiquidarVencidaSemLiquidacaoGauge = Metrics.CreateGauge(
        "custodia_guarda_a_liquidar_vencida_sem_liquidacao",
        "Guarda permanente 3/4 do F5: quantidade de a_liquidar vencida e não revertida sem liq:<fato>:brl — a " +
        "MESMA consulta que o job de liquidação usa para achar candidatas. Um job atrasado aponta para si " +
        "mesmo em vez de deixar o livro incompleto em silêncio.");

    private static readonly Gauge GuardaTributoDivergenteDoRederivadoGauge = Metrics.CreateGauge(
        "custodia_guarda_tributo_divergente_do_rederivado",
        "Guarda permanente 4/4 do F5: quantidade de resgates efetivos já tributados cujo IR/IOF re-derivado da " +
        "fila FIFO, com o corte posicional do próprio resgate, diverge das linhas ir:/iof: efetivas gravadas no " +
        "livro. Única das quatro que não é consulta de ausência — cobre compra retroativa, estorno de compra " +
        "já consumida e venda ou resgate retroativo que passa a preceder um resgate já tributado.");

    public void RegistrarGuardaResgatesSemAliq(int quantidade)
    {
        GuardaResgatesSemAliqGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Guarda F5 1/4: {Quantidade} resgate(s) efetivo(s) sem a linha aliq:<tradeId>. " +
                "Rode o backfill da janela F4→F5 (metade i) se ainda não rodou; reaparecer depois indica bug do handler.",
                quantidade);
        }
    }

    public void RegistrarGuardaAjustesDeResgateSemReversao(int quantidade)
    {
        GuardaAjustesDeResgateSemReversaoGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Guarda F5 2/4: {Quantidade} ajuste(s) sobre resgate sem o conjunto est: correspondente. " +
                "Rode o backfill da janela F4→F5 (metade ii, sempre depois da metade i) se ainda não rodou.",
                quantidade);
        }
    }

    public void RegistrarGuardaALiquidarVencidaSemLiquidacao(int quantidade)
    {
        GuardaALiquidarVencidaSemLiquidacaoGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Guarda F5 3/4: {Quantidade} a_liquidar vencida e não revertida sem liq:<fato>:brl. " +
                "O job de liquidação está atrasado ou parado.",
                quantidade);
        }
    }

    public void RegistrarGuardaTributoDivergenteDoRederivado(int quantidade)
    {
        GuardaTributoDivergenteDoRederivadoGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogCritical(
                "Guarda F5 4/4: {Quantidade} resgate(s) com tributo divergente do re-derivado pela fila FIFO. " +
                "Investigue compra retroativa, estorno de compra já consumida ou venda/resgate retroativo à " +
                "frente de um resgate já tributado. Conserto é estorno do resgate e relançamento, nunca UPDATE.",
                quantidade);
        }
    }

    public void RegistrarLiquidacaoCandidataInconsistente(string clienteId, string tradeId, string refExternaOfensora)
    {
        LiquidacaoCandidataInconsistenteTotal.Inc();

        logger.LogCritical(
            "Candidata de liquidação inconsistente pulada: cliente {ClienteId}, trade {TradeId}, ref_externa " +
            "ofensora {RefExternaOfensora} não tem o movimento principal correspondente. Estado impossível por " +
            "construção — investigue manualmente. O ciclo continuou para as demais candidatas.",
            clienteId,
            tradeId,
            refExternaOfensora);
    }

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

    public void RegistrarLiquidacaoLimitePorTeto(long candidatasEncontradas, long teto)
    {
        LiquidacaoLimitePorTetoTotal.Inc();

        logger.LogError(
            "Job de liquidação abortado: {CandidatasEncontradas} candidatas vencidas encontradas, acima do teto " +
            "configurado {Teto}. Nenhuma linha foi inserida neste ciclo — falha, não sucesso parcial.",
            candidatasEncontradas,
            teto);
    }

    public void RegistrarLiquidacaoCalendarioExaurido(string clienteId, string tradeId)
    {
        LiquidacaoCalendarioExauridoTotal.Inc();

        logger.LogCritical(
            "Job de liquidação encontrou o horizonte do calendário de dias úteis esgotado ao calcular D+1 útil: " +
            "cliente {ClienteId}, trade {TradeId}. Falha alta e proposital — semeie mais datas em " +
            "calendario_dias_uteis antes que o processamento dependente pare de vez.",
            clienteId,
            tradeId);
    }
}

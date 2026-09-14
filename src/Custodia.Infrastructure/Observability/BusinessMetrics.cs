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
        "custodia_conciliacao_resgates_sem_valor_a_receber",
        "Conciliação de resgates 1/4: quantidade de resgates efetivos (venda não revertida) sem a linha " +
        "aliq:<tradeId> — discriminador incondicional, gravado com ou sem tributo porque a_liquidar entra " +
        "BRUTO. Deveria ser sempre zero depois do reparo dos resgates antigos; reaparecer indica bug do handler ou backfill " +
        "pendente.");

    private static readonly Gauge GuardaAjustesDeResgateSemReversaoGauge = Metrics.CreateGauge(
        "custodia_conciliacao_estornos_sem_reversao_completa",
        "Conciliação de resgates 2/4: quantidade de ajustes sobre resgate cujo conjunto est: correspondente " +
        "ainda não foi gravado por inteiro (discriminado por est:aliq:, o mesmo padrão da guarda 1). Deveria " +
        "ser sempre zero depois do reparo dos resgates antigos.");

    private static readonly Gauge GuardaALiquidarVencidaSemLiquidacaoGauge = Metrics.CreateGauge(
        "custodia_conciliacao_dinheiro_nao_creditado",
        "Conciliação de resgates 3/4: quantidade de a_liquidar vencida e não revertida sem liq:<fato>:brl — a " +
        "MESMA consulta que o job de liquidação usa para achar candidatas. Um job atrasado aponta para si " +
        "mesmo em vez de deixar o livro incompleto em silêncio.");

    private static readonly Gauge GuardaTributoDivergenteDoRederivadoGauge = Metrics.CreateGauge(
        "custodia_conciliacao_tributo_divergente",
        "Conciliação de resgates 4/4: quantidade de resgates efetivos já tributados cujo IR/IOF re-derivado da " +
        "fila FIFO, com o corte posicional do próprio resgate, diverge das linhas ir:/iof: efetivas gravadas no " +
        "livro. Única das quatro que não é consulta de ausência — cobre compra retroativa, estorno de compra " +
        "já consumida e venda ou resgate retroativo que passa a preceder um resgate já tributado.");

    public void RegistrarGuardaResgatesSemAliq(int quantidade)
    {
        GuardaResgatesSemAliqGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Conciliação 1/4: {Quantidade} resgate(s) efetivo(s) sem a linha aliq:<tradeId>. " +
                "Rode `--reparar-resgates-antigos` se ainda não rodou; reaparecer depois indica bug do handler.",
                quantidade);
        }
    }

    public void RegistrarGuardaAjustesDeResgateSemReversao(int quantidade)
    {
        GuardaAjustesDeResgateSemReversaoGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Conciliação 2/4: {Quantidade} ajuste(s) sobre resgate sem o conjunto est: correspondente. " +
                "Rode `--reparar-resgates-antigos` se ainda não rodou (ele repara os estornos depois dos resgates, nessa ordem).",
                quantidade);
        }
    }

    public void RegistrarGuardaALiquidarVencidaSemLiquidacao(int quantidade)
    {
        GuardaALiquidarVencidaSemLiquidacaoGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Conciliação 3/4: {Quantidade} a_liquidar vencida e não revertida sem liq:<fato>:brl. " +
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
                "Conciliação 4/4: {Quantidade} resgate(s) com tributo divergente do re-derivado pela fila FIFO. " +
                "Investigue compra retroativa, estorno de compra já consumida ou venda/resgate retroativo à " +
                "frente de um resgate já tributado. Conserto é estorno do resgate e relançamento, nunca UPDATE.",
                quantidade);
        }
    }

    private static readonly Counter RevisaoDePrecoRecebidaTotal = Metrics.CreateCounter(
        "custodia_preco_revisao_recebida_total",
        "Total de revisões de preço (revisao > 0) recebidas via push ou drenagem — raras por definição " +
        "(ARQUITETURA §12); cada uma é uma correção de valor já publicado no histórico.",
        new CounterConfiguration { LabelNames = ["instrumento_id", "campo"] });

    private static readonly Counter ValorDivergenteNoHistoricoDePrecosTotal = Metrics.CreateCounter(
        "custodia_preco_valor_divergente_total",
        "Total de PriceObserved cuja chave natural (instrumento, data, campo, fonte, revisão) já existia no " +
        "histórico com um valor DIFERENTE — violação do contrato do Hub (mudança de valor exige revisao+1). " +
        "Nada é sobrescrito em nenhuma das duas tabelas.",
        new CounterConfiguration { LabelNames = ["instrumento_id", "campo"] });

    public void RegistrarRevisaoDePrecoRecebida(
        string instrumentoId, string campo, DateOnly dataRef, int revisao, string fonte, decimal valorNovo, decimal? valorAnterior)
    {
        RevisaoDePrecoRecebidaTotal.WithLabels(instrumentoId, campo).Inc();

        logger.LogWarning(
            "Revisão de preço recebida: instrumento {InstrumentoId}, campo {Campo}, data {DataRef}, revisão " +
            "{Revisao}, fonte {Fonte}, valor novo {ValorNovo}, valor anterior {ValorAnterior}. Correções são " +
            "raras (ARQUITETURA §12) e merecem visibilidade.",
            instrumentoId,
            campo,
            dataRef,
            revisao,
            fonte,
            valorNovo,
            valorAnterior);
    }

    public void RegistrarValorDivergenteNoHistoricoDePrecos(
        string instrumentoId, string campo, DateOnly dataRef, string fonte, int revisao, decimal valorAnterior, decimal valorNovo)
    {
        ValorDivergenteNoHistoricoDePrecosTotal.WithLabels(instrumentoId, campo).Inc();

        logger.LogCritical(
            "Valor divergente no histórico de preços: instrumento {InstrumentoId}, campo {Campo}, data {DataRef}, " +
            "fonte {Fonte}, revisão {Revisao}, valor já gravado {ValorAnterior}, valor recebido {ValorNovo}. " +
            "Mudança de valor exige revisao+1 (contrato do Hub) — nada foi sobrescrito.",
            instrumentoId,
            campo,
            dataRef,
            fonte,
            revisao,
            valorAnterior,
            valorNovo);
    }

    private static readonly Counter PrecoInstrumentoDesconhecidoTotal = Metrics.CreateCounter(
        "custodia_preco_instrumento_desconhecido_total",
        "Total de instrumentos do livro que o Hub de Preços não conhece ao consultar /v1/prices/asof " +
        "(motivo instrumento_desconhecido) — nunca fabricamos preço para eles; alerta, porque é sinal de defeito " +
        "de catálogo em algum dos dois lados.",
        new CounterConfiguration { LabelNames = ["instrumento_id"] });

    private static readonly Counter PrecoCampoPosicaoNaoInformadoTotal = Metrics.CreateCounter(
        "custodia_preco_campo_posicao_nao_informado_total",
        "Total de itens do /v1/prices/asof com campos de preço mas sem campoPosicao — defeito de classificação " +
        "no Hub (PADROES §10.32); histórico gravado, preco_atual não tocado.",
        new CounterConfiguration { LabelNames = ["instrumento_id"] });

    private static readonly Counter PrecoInstrumentoPosicionadoSemPrecoTotal = Metrics.CreateCounter(
        "custodia_preco_instrumento_posicionado_sem_preco_total",
        "Total de instrumentos posicionados sem preço encontrado na consulta ao Hub, rotulado por motivo: " +
        "sem_preco_ate_a_data (a fonte não tem preço até a data) ou campo_posicao_sem_preco (campoPosicao " +
        "informado mas ausente entre os campos devolvidos) — rótulos nunca fundidos.",
        new CounterConfiguration { LabelNames = ["instrumento_id", "motivo"] });

    private static readonly Gauge ConciliacaoDePrecosSemPrecoAtualGauge = Metrics.CreateGauge(
        "custodia_conciliacao_precos_sem_preco_atual",
        "Quantidade de instrumentos do livro (fora de caixa:) sem linha em preco_atual, medida na última volta " +
        "da conciliação cíclica de preços. Mantém viva a métrica de posicionado sem preço em regime.");

    private static readonly Counter ConciliacaoDePrecosVoltaTotal = Metrics.CreateCounter(
        "custodia_conciliacao_precos_volta_total",
        "Total de voltas da conciliação cíclica de preços, rotulado por desfecho (completude ou falha).",
        new CounterConfiguration { LabelNames = ["desfecho"] });

    public void RegistrarPrecoInstrumentoDesconhecido(string instrumentoId)
    {
        PrecoInstrumentoDesconhecidoTotal.WithLabels(instrumentoId).Inc();

        logger.LogError(
            "Instrumento {InstrumentoId} do livro é desconhecido no Hub de Preços ao consultar /v1/prices/asof. " +
            "Investigue catálogo/onboarding.",
            instrumentoId);
    }

    public void RegistrarPrecoCampoPosicaoNaoInformado(string instrumentoId)
    {
        PrecoCampoPosicaoNaoInformadoTotal.WithLabels(instrumentoId).Inc();

        logger.LogError(
            "Instrumento {InstrumentoId} veio do Hub de Preços com campos mas sem campoPosicao. Defeito de " +
            "classificação no Hub — histórico gravado, preco_atual não tocado.",
            instrumentoId);
    }

    public void RegistrarPrecoInstrumentoPosicionadoSemPreco(string instrumentoId, string motivo)
    {
        PrecoInstrumentoPosicionadoSemPrecoTotal.WithLabels(instrumentoId, motivo).Inc();

        logger.LogWarning(
            "Instrumento {InstrumentoId} posicionado sem preço encontrado ({Motivo}).",
            instrumentoId,
            motivo);
    }

    public void RegistrarConciliacaoDePrecosSemPrecoAtual(int quantidade)
    {
        ConciliacaoDePrecosSemPrecoAtualGauge.Set(quantidade);

        if (quantidade > 0)
        {
            logger.LogWarning(
                "Conciliação de preços: {Quantidade} instrumento(s) do livro ainda sem preco_atual.",
                quantidade);
        }
    }

    public void RegistrarConciliacaoDePrecosVolta(string desfecho) =>
        ConciliacaoDePrecosVoltaTotal.WithLabels(desfecho).Inc();

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

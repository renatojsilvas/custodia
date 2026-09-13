using Custodia.Application.Common.Interfaces;

namespace Custodia.Application.Tests.Fakes;

internal sealed record SinalizacaoDePosicaoNegativa(string ClienteId, string InstrumentoId, decimal QuantidadeResultante);

internal sealed record RegistroDeHorizonteCalendario(int DiasRestantes, int DiasMinimosConfigurados);

internal sealed record SinalizacaoDeResgateSobrePrecoMedioProvisorio(
    string ClienteId, string InstrumentoId, decimal QuantidadeDescoberta);

internal sealed record LimitePorTetoDeLiquidacao(long CandidatasEncontradas, long Teto);

internal sealed record CalendarioExauridoNaLiquidacao(string ClienteId, string TradeId);

internal sealed record CandidataInconsistenteNaLiquidacao(string ClienteId, string TradeId, string RefExternaOfensora);

internal sealed class FakeBusinessMetrics : IBusinessMetrics
{
    public List<SinalizacaoDePosicaoNegativa> Sinalizacoes { get; } = [];

    public List<RegistroDeHorizonteCalendario> RegistrosDeHorizonteCalendario { get; } = [];

    public List<SinalizacaoDeResgateSobrePrecoMedioProvisorio> SinalizacoesDeResgateSobrePrecoMedioProvisorio { get; } = [];

    public List<LimitePorTetoDeLiquidacao> LimitesPorTetoDeLiquidacao { get; } = [];

    public List<CalendarioExauridoNaLiquidacao> CalendariosExauridosNaLiquidacao { get; } = [];

    public List<CandidataInconsistenteNaLiquidacao> CandidatasInconsistentesNaLiquidacao { get; } = [];

    public void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante) =>
        Sinalizacoes.Add(new SinalizacaoDePosicaoNegativa(clienteId, instrumentoId, quantidadeResultante));

    public void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados) =>
        RegistrosDeHorizonteCalendario.Add(new RegistroDeHorizonteCalendario(diasRestantes, diasMinimosConfigurados));

    public void RegistrarResgateTributadoSobrePrecoMedioProvisorio(
        string clienteId, string instrumentoId, decimal quantidadeDescoberta) =>
        SinalizacoesDeResgateSobrePrecoMedioProvisorio.Add(
            new SinalizacaoDeResgateSobrePrecoMedioProvisorio(clienteId, instrumentoId, quantidadeDescoberta));

    public void RegistrarLiquidacaoLimitePorTeto(long candidatasEncontradas, long teto) =>
        LimitesPorTetoDeLiquidacao.Add(new LimitePorTetoDeLiquidacao(candidatasEncontradas, teto));

    public void RegistrarLiquidacaoCalendarioExaurido(string clienteId, string tradeId) =>
        CalendariosExauridosNaLiquidacao.Add(new CalendarioExauridoNaLiquidacao(clienteId, tradeId));

    public void RegistrarLiquidacaoCandidataInconsistente(string clienteId, string tradeId, string refExternaOfensora) =>
        CandidatasInconsistentesNaLiquidacao.Add(new CandidataInconsistenteNaLiquidacao(clienteId, tradeId, refExternaOfensora));

    public List<int> GuardaResgatesSemAliq { get; } = [];

    public List<int> GuardaAjustesDeResgateSemReversao { get; } = [];

    public List<int> GuardaALiquidarVencidaSemLiquidacao { get; } = [];

    public List<int> GuardaTributoDivergenteDoRederivado { get; } = [];

    public void RegistrarGuardaResgatesSemAliq(int quantidade) => GuardaResgatesSemAliq.Add(quantidade);

    public void RegistrarGuardaAjustesDeResgateSemReversao(int quantidade) => GuardaAjustesDeResgateSemReversao.Add(quantidade);

    public void RegistrarGuardaALiquidarVencidaSemLiquidacao(int quantidade) => GuardaALiquidarVencidaSemLiquidacao.Add(quantidade);

    public void RegistrarGuardaTributoDivergenteDoRederivado(int quantidade) => GuardaTributoDivergenteDoRederivado.Add(quantidade);
}

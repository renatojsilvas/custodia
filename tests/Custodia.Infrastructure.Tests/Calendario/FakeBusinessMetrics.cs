using Custodia.Application.Common.Interfaces;

namespace Custodia.Infrastructure.Tests.Calendario;

internal sealed record RegistroDeHorizonte(int DiasRestantes, int DiasMinimosConfigurados);

internal sealed record CandidataInconsistenteRegistrada(string ClienteId, string TradeId, string RefExternaOfensora);

internal sealed class FakeBusinessMetrics : IBusinessMetrics
{
    public List<RegistroDeHorizonte> Registros { get; } = [];

    public List<CandidataInconsistenteRegistrada> CandidatasInconsistentes { get; } = [];

    public void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante)
    {
    }

    public void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados) =>
        Registros.Add(new RegistroDeHorizonte(diasRestantes, diasMinimosConfigurados));

    public void RegistrarResgateTributadoSobrePrecoMedioProvisorio(
        string clienteId, string instrumentoId, decimal quantidadeDescoberta)
    {
    }

    public void RegistrarLiquidacaoLimitePorTeto(long candidatasEncontradas, long teto)
    {
    }

    public void RegistrarLiquidacaoCalendarioExaurido(string clienteId, string tradeId)
    {
    }

    public void RegistrarLiquidacaoCandidataInconsistente(string clienteId, string tradeId, string refExternaOfensora) =>
        CandidatasInconsistentes.Add(new CandidataInconsistenteRegistrada(clienteId, tradeId, refExternaOfensora));

    public void RegistrarRevisaoDePrecoRecebida(
        string instrumentoId, string campo, DateOnly dataRef, int revisao, string fonte, decimal valorNovo, decimal? valorAnterior)
    {
    }

    public void RegistrarValorDivergenteNoHistoricoDePrecos(
        string instrumentoId, string campo, DateOnly dataRef, string fonte, int revisao, decimal valorAnterior, decimal valorNovo)
    {
    }

    public List<int> GuardaResgatesSemAliq { get; } = [];

    public List<int> GuardaAjustesDeResgateSemReversao { get; } = [];

    public List<int> GuardaALiquidarVencidaSemLiquidacao { get; } = [];

    public List<int> GuardaTributoDivergenteDoRederivado { get; } = [];

    public void RegistrarGuardaResgatesSemAliq(int quantidade) => GuardaResgatesSemAliq.Add(quantidade);

    public void RegistrarGuardaAjustesDeResgateSemReversao(int quantidade) => GuardaAjustesDeResgateSemReversao.Add(quantidade);

    public void RegistrarGuardaALiquidarVencidaSemLiquidacao(int quantidade) => GuardaALiquidarVencidaSemLiquidacao.Add(quantidade);

    public void RegistrarGuardaTributoDivergenteDoRederivado(int quantidade) => GuardaTributoDivergenteDoRederivado.Add(quantidade);
}

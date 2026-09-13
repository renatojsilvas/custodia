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
}

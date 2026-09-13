namespace Custodia.Application.Common.Interfaces;

public interface IBusinessMetrics
{
    void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante);

    void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados);

    void RegistrarResgateTributadoSobrePrecoMedioProvisorio(string clienteId, string instrumentoId, decimal quantidadeDescoberta);

    void RegistrarLiquidacaoLimitePorTeto(long candidatasEncontradas, long teto);

    void RegistrarLiquidacaoCalendarioExaurido(string clienteId, string tradeId);

    void RegistrarLiquidacaoCandidataInconsistente(string clienteId, string tradeId, string refExternaOfensora);
}

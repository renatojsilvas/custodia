namespace Custodia.Application.Common.Interfaces;

public interface IBusinessMetrics
{
    void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante);

    void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados);

    void RegistrarResgateTributadoSobrePrecoMedioProvisorio(string clienteId, string instrumentoId, decimal quantidadeDescoberta);
}

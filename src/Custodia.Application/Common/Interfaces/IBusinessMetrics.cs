namespace Custodia.Application.Common.Interfaces;

public interface IBusinessMetrics
{
    void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante);
}

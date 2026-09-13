using Custodia.Application.Common.Interfaces;

namespace Custodia.Infrastructure.Tests.Calendario;

internal sealed record RegistroDeHorizonte(int DiasRestantes, int DiasMinimosConfigurados);

internal sealed class FakeBusinessMetrics : IBusinessMetrics
{
    public List<RegistroDeHorizonte> Registros { get; } = [];

    public void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante)
    {
    }

    public void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados) =>
        Registros.Add(new RegistroDeHorizonte(diasRestantes, diasMinimosConfigurados));

    public void RegistrarResgateTributadoSobrePrecoMedioProvisorio(
        string clienteId, string instrumentoId, decimal quantidadeDescoberta)
    {
    }
}

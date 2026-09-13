using Custodia.Application.Liquidacao;

namespace Custodia.Application.Common.Interfaces;

public interface IPausaEntreLerEGravar
{
    Task AposLerAntesDeGravarAsync(string clienteId, string refExterna, CancellationToken ct);
}

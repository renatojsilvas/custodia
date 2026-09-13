using Custodia.Application.Common.Interfaces;

using Custodia.Application.Liquidacao;

namespace Custodia.Infrastructure.Common;

public sealed class PausaEntreLerEGravarInerte : IPausaEntreLerEGravar
{
    public Task AposLerAntesDeGravarAsync(string clienteId, string refExterna, CancellationToken ct) => Task.CompletedTask;
}

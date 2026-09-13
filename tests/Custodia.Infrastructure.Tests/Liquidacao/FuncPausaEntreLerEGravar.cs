using Custodia.Application.Common.Interfaces;

using Custodia.Application.Liquidacao;

namespace Custodia.Infrastructure.Tests.Liquidacao;

internal sealed class FuncPausaEntreLerEGravar(Func<string, string, CancellationToken, Task> acao)
    : IPausaEntreLerEGravar
{
    public Task AposLerAntesDeGravarAsync(string clienteId, string refExterna, CancellationToken ct) => acao(clienteId, refExterna, ct);
}

using Custodia.Application.Common.Interfaces;

using Custodia.Application.Liquidacao;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakePausaEntreLerEGravar(Func<string, string, CancellationToken, Task>? acao = null)
    : IPausaEntreLerEGravar
{
    private readonly Func<string, string, CancellationToken, Task> _acao = acao ?? ((_, _, _) => Task.CompletedTask);

    public Task AposLerAntesDeGravarAsync(string clienteId, string refExterna, CancellationToken ct) => _acao(clienteId, refExterna, ct);
}

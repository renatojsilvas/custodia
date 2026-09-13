using Custodia.Application.Common.Interfaces;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakePontoDeSuspensaoAposTravamento(Func<string, string, CancellationToken, Task>? acao = null)
    : IPontoDeSuspensaoAposTravamento
{
    private readonly Func<string, string, CancellationToken, Task> _acao = acao ?? ((_, _, _) => Task.CompletedTask);

    public Task AposTravarAsync(string clienteId, string refExterna, CancellationToken ct) => _acao(clienteId, refExterna, ct);
}

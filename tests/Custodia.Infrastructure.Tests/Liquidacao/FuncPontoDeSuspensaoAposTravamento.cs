using Custodia.Application.Common.Interfaces;

namespace Custodia.Infrastructure.Tests.Liquidacao;

internal sealed class FuncPontoDeSuspensaoAposTravamento(Func<string, string, CancellationToken, Task> acao)
    : IPontoDeSuspensaoAposTravamento
{
    public Task AposTravarAsync(string clienteId, string refExterna, CancellationToken ct) => acao(clienteId, refExterna, ct);
}

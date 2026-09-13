using Custodia.Application.Common.Interfaces;

namespace Custodia.Infrastructure.Common;

public sealed class PontoDeSuspensaoAposTravamentoInerte : IPontoDeSuspensaoAposTravamento
{
    public Task AposTravarAsync(string clienteId, string refExterna, CancellationToken ct) => Task.CompletedTask;
}

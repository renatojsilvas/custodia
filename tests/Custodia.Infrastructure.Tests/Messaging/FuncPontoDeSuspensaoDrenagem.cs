using Custodia.Infrastructure.Messaging;

namespace Custodia.Infrastructure.Tests.Messaging;

internal sealed class FuncPontoDeSuspensaoDrenagem(Func<CancellationToken, Task> acao) : IPontoDeSuspensaoDrenagem
{
    public Task AposLerEstoqueAsync(CancellationToken ct) => acao(ct);
}

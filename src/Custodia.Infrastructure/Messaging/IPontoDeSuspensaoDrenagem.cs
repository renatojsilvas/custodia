namespace Custodia.Infrastructure.Messaging;

public interface IPontoDeSuspensaoDrenagem
{
    Task AposLerEstoqueAsync(CancellationToken ct);
}

public sealed class PontoDeSuspensaoDrenagemInerte : IPontoDeSuspensaoDrenagem
{
    public Task AposLerEstoqueAsync(CancellationToken ct) => Task.CompletedTask;
}

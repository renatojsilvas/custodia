namespace Custodia.Infrastructure.Messaging;

public interface IPublicadorComConfirmacao
{
    Task<bool> PublicarAsync(
        string exchange,
        string routingKey,
        IDictionary<string, object?> cabecalhos,
        ReadOnlyMemory<byte> corpo,
        CancellationToken ct);
}

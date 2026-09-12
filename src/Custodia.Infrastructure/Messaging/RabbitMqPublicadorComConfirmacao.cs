using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Custodia.Infrastructure.Messaging;

public sealed class RabbitMqPublicadorComConfirmacao(
    RabbitMqConnectionProvider connectionProvider, ILogger<RabbitMqPublicadorComConfirmacao> logger)
    : IPublicadorComConfirmacao
{
    public async Task<bool> PublicarAsync(
        string exchange,
        string routingKey,
        IDictionary<string, object?> cabecalhos,
        ReadOnlyMemory<byte> corpo,
        CancellationToken ct)
    {
        var conexao = await connectionProvider.ObterConexaoAsync(ct);

        await using var canal = await conexao.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        var propriedades = new BasicProperties
        {
            Headers = cabecalhos,
            Persistent = true,
        };

        try
        {
            await canal.BasicPublishAsync(exchange, routingKey, mandatory: false, propriedades, corpo, ct);
            return true;
        }
        catch (PublishException ex)
        {
            logger.LogWarning(ex, "Publisher confirm negado ao republicar no exchange {Exchange}.", exchange);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao republicar no exchange {Exchange} (broker indisponível ou timeout).", exchange);
            return false;
        }
    }
}

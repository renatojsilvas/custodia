using RabbitMQ.Client;

namespace Custodia.Infrastructure.Messaging;

public static class RabbitMqTopologia
{
    public const string ExchangePrices = "prices";
    public const string ExchangeDlx = "custodia.dlx";
    public const string ExchangeParking = "custodia.parking";
    public const string ExchangeRetryIn = "custodia.retry.in";
    public const string ExchangeRetryDlx = "custodia.retry.dlx";

    public const string FilaPrincipal = "custodia.prices";
    public const string FilaDlq = "custodia.prices.dlq";
    public const string FilaParked = "custodia.parked";
    public const string FilaRetry = "custodia.retry";

    private const long DeliveryLimitPrincipal = 20;
    private const long DeliveryLimitIlimitado = -1;
    private const int RetryTtlMs = 30000;

    public static async Task DeclararAsync(IChannel canal, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(
            ExchangePrices, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await DeclararExchangeFanoutAsync(canal, ExchangeDlx, ct);
        await DeclararExchangeFanoutAsync(canal, ExchangeParking, ct);
        await DeclararExchangeFanoutAsync(canal, ExchangeRetryIn, ct);
        await DeclararExchangeFanoutAsync(canal, ExchangeRetryDlx, ct);

        await canal.QueueDeclareAsync(
            FilaPrincipal,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = DeliveryLimitPrincipal,
                ["x-dead-letter-exchange"] = ExchangeDlx,
            },
            cancellationToken: ct);

        await canal.QueueDeclareAsync(
            FilaDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = DeliveryLimitIlimitado,
            },
            cancellationToken: ct);

        await canal.QueueDeclareAsync(
            FilaParked,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = DeliveryLimitIlimitado,
            },
            cancellationToken: ct);

        await canal.QueueDeclareAsync(
            FilaRetry,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "classic",
                ["x-message-ttl"] = RetryTtlMs,
                ["x-dead-letter-exchange"] = ExchangeRetryDlx,
            },
            cancellationToken: ct);

        await canal.QueueBindAsync(FilaPrincipal, ExchangePrices, "prices.#", cancellationToken: ct);
        await canal.QueueBindAsync(FilaPrincipal, ExchangePrices, "corpactions.#", cancellationToken: ct);
        await canal.QueueBindAsync(FilaPrincipal, ExchangePrices, "eod.ready", cancellationToken: ct);
        await canal.QueueBindAsync(FilaPrincipal, ExchangePrices, "trades.registered", cancellationToken: ct);
        await canal.QueueBindAsync(FilaDlq, ExchangeDlx, string.Empty, cancellationToken: ct);
        await canal.QueueBindAsync(FilaParked, ExchangeParking, string.Empty, cancellationToken: ct);
        await canal.QueueBindAsync(FilaRetry, ExchangeRetryIn, string.Empty, cancellationToken: ct);
        await canal.QueueBindAsync(FilaPrincipal, ExchangeRetryDlx, string.Empty, cancellationToken: ct);
    }

    private static Task DeclararExchangeFanoutAsync(IChannel canal, string nome, CancellationToken ct) =>
        canal.ExchangeDeclareAsync(nome, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
}

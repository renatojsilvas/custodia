using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Messaging;

public sealed class RabbitMqFilaProfundidadePoller(
    RabbitMqConnectionProvider connectionProvider,
    ConsumidorMetrics metrics,
    ILogger<RabbitMqFilaProfundidadePoller> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(15);

    private static readonly string[] Filas =
    [
        RabbitMqTopologia.FilaPrincipal,
        RabbitMqTopologia.FilaDlq,
        RabbitMqTopologia.FilaRetry,
        RabbitMqTopologia.FilaParked,
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);

        do
        {
            await MedirUmaVezAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task MedirUmaVezAsync(CancellationToken ct)
    {
        try
        {
            var conexao = await connectionProvider.ObterConexaoAsync(ct);
            await using var canal = await conexao.CreateChannelAsync(cancellationToken: ct);

            foreach (var fila in Filas)
            {
                var quantidade = await canal.MessageCountAsync(fila, ct);
                metrics.RegistrarProfundidadeDaFila(fila, (long)quantidade);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao medir a profundidade das filas da custódia; nova tentativa no próximo ciclo.");
        }
    }
}

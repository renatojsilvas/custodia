using Custodia.Application.Liquidacao;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Liquidacao;

public sealed class FilaDeRecalculoInerte(ILogger<FilaDeRecalculoInerte> logger) : IFilaDeRecalculo
{
    public Task EnfileirarAsync(string clienteId, string instrumentoId, DateOnly desde, CancellationToken ct)
    {
        logger.LogInformation(
            "Recálculo retroativo enfileirado (porta ainda sem worker no F7): cliente {ClienteId}, " +
            "instrumento {InstrumentoId}, desde {Desde}. Caminho rápido, não garantia — a garantia é a " +
            "varredura de defasagem de snapshot do F7.",
            clienteId,
            instrumentoId,
            desde);

        return Task.CompletedTask;
    }
}

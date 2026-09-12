using Custodia.Domain.Eventos;

namespace Custodia.Infrastructure.Messaging;

public interface IMensagemParkeadaReprocessador
{
    Task<ResultadoReprocessamento> ReprocessarAsync(
        string routingKey, ReadOnlyMemory<byte> corpo, MotivoEstacionamento motivoOriginal, CancellationToken ct);
}

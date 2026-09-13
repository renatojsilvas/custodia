using Custodia.Domain.Eventos;

namespace Custodia.Infrastructure.Messaging;

public interface IMensagemParkingReprocessador
{
    Task<ResultadoReprocessamento> ReprocessarAsync(
        string routingKey, ReadOnlyMemory<byte> corpo, MotivoParking motivoOriginal, CancellationToken ct);
}

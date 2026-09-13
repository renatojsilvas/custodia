namespace Custodia.Application.Liquidacao;

public interface IRecalculoEnfileiradorPort
{
    Task EnfileirarAsync(string clienteId, string instrumentoId, DateOnly desde, CancellationToken ct);
}

namespace Custodia.Application.Liquidacao;

public interface IFilaDeRecalculo
{
    Task EnfileirarAsync(string clienteId, string instrumentoId, DateOnly desde, CancellationToken ct);
}

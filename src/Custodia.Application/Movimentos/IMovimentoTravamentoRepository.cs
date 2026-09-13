using Custodia.Domain.Common;

namespace Custodia.Application.Movimentos;

public interface IMovimentoTravamentoRepository
{
    Task<Result<MovimentoConsulta>> TravarPorClienteERefExternaAsync(
        string clienteId, string refExterna, CancellationToken ct);
}

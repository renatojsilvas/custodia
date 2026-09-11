using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Application.Movimentos;

public interface IMovimentoReadRepository
{
    Task<Result<MovimentoConsulta>> ObterPorClienteERefExternaAsync(
        string clienteId, string refExterna, CancellationToken ct);

    Task<Result<MovimentoConsulta>> ObterPorRefExternaEmQualquerClienteAsync(
        string refExterna, CancellationToken ct);

    Task<Result<MaxDataEventoConsulta>> ObterMaxDataEventoAsync(
        string clienteId, string instrumentoId, CancellationToken ct);

    Task<Result<IReadOnlyList<Movimento>>> ObterMovimentosDaChaveAsync(
        string clienteId, string instrumentoId, CancellationToken ct);
}

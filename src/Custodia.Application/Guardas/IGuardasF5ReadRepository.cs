using Custodia.Domain.Common;

namespace Custodia.Application.Guardas;

public interface IGuardasF5ReadRepository
{
    Task<Result<IReadOnlyList<ResgateTributado>>> ObterResgatesTributadosAsync(CancellationToken ct);
}

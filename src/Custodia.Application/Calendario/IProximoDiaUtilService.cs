using Custodia.Domain.Common;

namespace Custodia.Application.Calendario;

public interface IProximoDiaUtilService
{
    Task<Result<DateOnly>> ProximoDiaUtilAsync(DateOnly data, CancellationToken ct);
}

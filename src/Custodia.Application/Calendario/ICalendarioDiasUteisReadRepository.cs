using Custodia.Domain.Common;

namespace Custodia.Application.Calendario;

public interface ICalendarioDiasUteisReadRepository
{
    Task<Result<ProximoDiaUtilConsulta>> ObterProximoDiaUtilAsync(DateOnly data, CancellationToken ct);

    Task<Result<HorizonteCalendarioConsulta>> ObterHorizonteAsync(CancellationToken ct);
}

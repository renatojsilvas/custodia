using Custodia.Application.Calendario;
using Custodia.Domain.Common;

namespace Custodia.Infrastructure.Tests.Calendario;

internal sealed class FakeCalendarioDiasUteisReadRepository(Func<Result<HorizonteCalendarioConsulta>> horizonte)
    : ICalendarioDiasUteisReadRepository
{
    public Task<Result<ProximoDiaUtilConsulta>> ObterProximoDiaUtilAsync(DateOnly data, CancellationToken ct) =>
        throw new NotSupportedException("Não usado pelo CalendarioDiasUteisHorizonteGuard.");

    public Task<Result<HorizonteCalendarioConsulta>> ObterHorizonteAsync(CancellationToken ct) =>
        Task.FromResult(horizonte());
}

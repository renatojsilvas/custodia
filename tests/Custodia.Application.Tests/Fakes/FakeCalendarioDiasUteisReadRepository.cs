using Custodia.Application.Calendario;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeCalendarioDiasUteisReadRepository(
    Func<DateOnly, Result<ProximoDiaUtilConsulta>>? proximoDiaUtil = null,
    Func<Result<HorizonteCalendarioConsulta>>? horizonte = null)
    : ICalendarioDiasUteisReadRepository
{
    private readonly Func<DateOnly, Result<ProximoDiaUtilConsulta>> _proximoDiaUtil =
        proximoDiaUtil ?? (_ => Result<ProximoDiaUtilConsulta>.Success(ProximoDiaUtilConsulta.NaoEncontrado));

    private readonly Func<Result<HorizonteCalendarioConsulta>> _horizonte =
        horizonte ?? (() => Result<HorizonteCalendarioConsulta>.Success(new HorizonteCalendarioConsulta(null, default)));

    public Task<Result<ProximoDiaUtilConsulta>> ObterProximoDiaUtilAsync(DateOnly data, CancellationToken ct) =>
        Task.FromResult(_proximoDiaUtil(data));

    public Task<Result<HorizonteCalendarioConsulta>> ObterHorizonteAsync(CancellationToken ct) =>
        Task.FromResult(_horizonte());
}

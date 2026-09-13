using Custodia.Domain.Calendario;
using Custodia.Domain.Common;

namespace Custodia.Application.Calendario;

public sealed class ProximoDiaUtilService(ICalendarioDiasUteisReadRepository repositorio) : IProximoDiaUtilService
{
    public async Task<Result<DateOnly>> ProximoDiaUtilAsync(DateOnly data, CancellationToken ct)
    {
        var resultado = await repositorio.ObterProximoDiaUtilAsync(data, ct);
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        return resultado.Value.Encontrado
            ? Result<DateOnly>.Success(resultado.Value.Valor)
            : Result<DateOnly>.Failure(CalendarioDiasUteisErrors.HorizonteEsgotado);
    }
}

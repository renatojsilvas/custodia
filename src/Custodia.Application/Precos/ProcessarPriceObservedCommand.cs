using Custodia.Application.Eventos;
using Custodia.Domain.Common;
using MediatR;

namespace Custodia.Application.Precos;

public sealed record ProcessarPriceObservedCommand(PriceObservedEvento Evento)
    : IRequest<Result<ResultadoPriceObserved>>;

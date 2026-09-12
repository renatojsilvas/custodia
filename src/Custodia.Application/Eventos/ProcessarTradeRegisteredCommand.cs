using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Eventos;

public sealed record ProcessarTradeRegisteredCommand(TradeRegisteredEvento Evento)
    : IRequest<Result<ResultadoTradeRegistered>>;

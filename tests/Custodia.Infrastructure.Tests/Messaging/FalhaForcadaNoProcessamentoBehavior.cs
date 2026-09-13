using System.Collections.Concurrent;
using Custodia.Application.Eventos;
using Custodia.Domain.Common;
using MediatR;

namespace Custodia.Infrastructure.Tests.Messaging;

internal sealed class FalhaForcadaNoProcessamentoBehavior(Func<TradeRegisteredEvento, int, Exception?> decidirFalha)
    : IPipelineBehavior<ProcessarTradeRegisteredCommand, Result<ResultadoTradeRegistered>>
{
    private readonly ConcurrentDictionary<string, int> _tentativasPorTradeId = new();

    public async Task<Result<ResultadoTradeRegistered>> Handle(
        ProcessarTradeRegisteredCommand request,
        RequestHandlerDelegate<Result<ResultadoTradeRegistered>> next,
        CancellationToken cancellationToken)
    {
        var tentativa = _tentativasPorTradeId.AddOrUpdate(request.Evento.TradeId, 1, (_, atual) => atual + 1);
        var excecaoForcada = decidirFalha(request.Evento, tentativa);

        if (excecaoForcada is not null)
        {
            throw excecaoForcada;
        }

        return await next(cancellationToken);
    }
}

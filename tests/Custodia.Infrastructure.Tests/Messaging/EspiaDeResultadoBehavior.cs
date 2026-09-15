using Custodia.Application.Eventos;
using Custodia.Domain.Common;
using MediatR;

namespace Custodia.Infrastructure.Tests.Messaging;

internal sealed class EspiaDeResultadoBehavior
    : IPipelineBehavior<ProcessarTradeRegisteredCommand, Result<ResultadoTradeRegistered>>
{
    private readonly object _cadeado = new();
    private Result<ResultadoTradeRegistered>? _ultimoResultado;
    private Exception? _ultimaExcecao;

    public Result<ResultadoTradeRegistered>? UltimoResultado
    {
        get
        {
            lock (_cadeado)
            {
                return _ultimoResultado;
            }
        }
    }

    public Exception? UltimaExcecao
    {
        get
        {
            lock (_cadeado)
            {
                return _ultimaExcecao;
            }
        }
    }

    public async Task<Result<ResultadoTradeRegistered>> Handle(
        ProcessarTradeRegisteredCommand request,
        RequestHandlerDelegate<Result<ResultadoTradeRegistered>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await next(cancellationToken);
            lock (_cadeado)
            {
                _ultimoResultado = resultado;
            }

            return resultado;
        }
        catch (Exception excecao)
        {
            lock (_cadeado)
            {
                _ultimaExcecao = excecao;
            }

            throw;
        }
    }
}

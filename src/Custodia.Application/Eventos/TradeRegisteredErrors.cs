using Custodia.Domain.Common;

namespace Custodia.Application.Eventos;

public static class TradeRegisteredErrors
{
    public static readonly Error VersaoNaoSuportada =
        new("TradeRegistered.VersaoNaoSuportada", "Versão do envelope TradeRegistered não suportada.", ErrorType.Unprocessable);

    public static readonly Error PayloadInvalido =
        new("TradeRegistered.PayloadInvalido", "Payload do TradeRegistered inválido.", ErrorType.Unprocessable);
}

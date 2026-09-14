using Custodia.Domain.Common;

namespace Custodia.Application.Eventos;

public static class PriceObservedErrors
{
    public static readonly Error VersaoNaoSuportada =
        new("PriceObserved.VersaoNaoSuportada", "Versão do envelope PriceObserved não suportada.", ErrorType.Unprocessable);

    public static readonly Error PayloadInvalido =
        new("PriceObserved.PayloadInvalido", "Payload do PriceObserved inválido.", ErrorType.Unprocessable);
}

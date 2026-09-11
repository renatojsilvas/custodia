using Custodia.Domain.Common;

namespace Custodia.Domain.Movimentos;

public static class MovimentoErrors
{
    public static readonly Error TipoInvalido =
        new("TipoMovimento.Invalido", "Tipo de movimento inválido.", ErrorType.Unprocessable);
}

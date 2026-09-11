using Custodia.Domain.Common;

namespace Custodia.Application.Movimentos;

public static class MovimentoWriteErrors
{
    public static readonly Error RefEstornoDuplicado =
        new(
            "Movimento.RefEstornoDuplicado",
            "Já existe um ajuste revertendo este movimento (ix_movimentos_ref_estorno_unico).",
            ErrorType.Conflict);

    public static readonly Error IdentificadorComEspacoNaBorda =
        new(
            "Movimento.IdentificadorComEspacoNaBorda",
            "cliente_id, instrumento_id ou ref_externa com espaço, tab ou newline na borda.",
            ErrorType.Conflict);
}

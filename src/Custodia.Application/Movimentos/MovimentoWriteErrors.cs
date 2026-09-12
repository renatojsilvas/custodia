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

    public static readonly Error MensagemDuplicada =
        new(
            "Movimento.MensagemDuplicada",
            "Já existe um movimento com este (cliente_id, ref_externa) (ix_movimentos_cliente_ref_externa) — " +
            "reentrega da mesma mensagem já processada por outra execução concorrente; desfecho correto é no-op.",
            ErrorType.Conflict);

    public static readonly Error ValorNumericoExcedeMagnitudeOuEscalaSuportada =
        new(
            "Movimento.ValorNumericoExcedeMagnitudeOuEscalaSuportada",
            "Valor numérico excede a magnitude ou a escala suportada pela coluna (numeric_field_overflow).",
            ErrorType.Unprocessable);

    public static readonly Error DataEventoFutura =
        new(
            "Movimento.DataEventoFutura",
            "data_evento é futura em relação a hoje em America/Sao_Paulo (trg_movimentos_data_evento_futura).",
            ErrorType.Unprocessable);

    public static readonly Error OperacaoNaoPermitidaSobreMovimentoImutavel =
        new(
            "Movimento.OperacaoNaoPermitidaSobreMovimentoImutavel",
            "movimentos é append-only: UPDATE/DELETE não são permitidos (trg_movimentos_imutavel). " +
            "Isto não deveria acontecer — é defeito nosso, não do chamador.",
            ErrorType.Unprocessable);
}

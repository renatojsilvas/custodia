using Custodia.Domain.Common;

namespace Custodia.Application.Liquidacao;

public static class LiquidacaoErrors
{
    public static readonly Error LimiteDeCandidatasPorCicloExcedido =
        new(
            "Liquidacao.LimiteDeCandidatasPorCicloExcedido",
            "O número de linhas a_liquidar vencidas e abertas excede o teto configurado por ciclo. " +
            "Falha alta — nenhuma linha foi liquidada neste ciclo, nunca sucesso parcial (PADROES 10.31).",
            ErrorType.Unprocessable);

    public static readonly Error CalendarioExaurido =
        new(
            "Liquidacao.CalendarioExaurido",
            "O horizonte do calendário de dias úteis está esgotado para calcular D+1 útil de uma linha " +
            "a_liquidar vencida. Falha alta e proposital — nunca degrada para dia corrido.",
            ErrorType.Unprocessable);

    public static readonly Error PayloadDeLiquidacaoInvalido =
        new(
            "Liquidacao.PayloadDeLiquidacaoInvalido",
            "O saldo do fato em caixa:a_liquidar excede a magnitude ou a escala suportada pela coluna " +
            "ao construir as duas pernas de liquidacao.",
            ErrorType.Unprocessable);

    public static readonly Error MovimentoPrincipalNaoEncontrado =
        new(
            "Liquidacao.MovimentoPrincipalNaoEncontrado",
            "Existe a_liquidar sem o movimento principal (venda) correspondente pela ref_externa — " +
            "estado inconsistente do livro. Isto não deveria acontecer.",
            ErrorType.Unprocessable);
}

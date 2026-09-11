using Custodia.Domain.Common;

namespace Custodia.Domain.Movimentos;

public static class MovimentoErrors
{
    public static readonly Error TipoInvalido =
        new("TipoMovimento.Invalido", "Tipo de movimento inválido.", ErrorType.Unprocessable);

    public static readonly Error QtdDeltaExcedePrecisaoSuportada =
        new(
            "Movimento.QtdDeltaExcedePrecisaoSuportada",
            $"QtdDelta deve ter no máximo {SchemaNumericLimits.QuantidadePrecisao - SchemaNumericLimits.QuantidadeEscala} " +
            $"dígitos inteiros e {SchemaNumericLimits.QuantidadeEscala} dígitos decimais.",
            ErrorType.Unprocessable);

    public static readonly Error ValorFinanceiroExcedePrecisaoSuportada =
        new(
            "Movimento.ValorFinanceiroExcedePrecisaoSuportada",
            $"ValorFinanceiro deve ter no máximo {SchemaNumericLimits.ValorPrecisao - SchemaNumericLimits.ValorEscala} " +
            $"dígitos inteiros e {SchemaNumericLimits.ValorEscala} dígitos decimais.",
            ErrorType.Unprocessable);
}

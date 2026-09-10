namespace Custodia.Domain.Common;

public static class SchemaNumericLimits
{
    public const int QuantidadePrecisao = 18;

    public const int QuantidadeEscala = 8;

    public const int ValorPrecisao = 18;

    public const int ValorEscala = 2;

    public const int PrecoPrecisao = 18;

    public const int PrecoEscala = 6;

    public static readonly decimal QuantidadeLimiteSuperiorExclusivo =
        Potencia10(QuantidadePrecisao - QuantidadeEscala);

    public static readonly decimal ValorLimiteSuperiorExclusivo =
        Potencia10(ValorPrecisao - ValorEscala);

    public static readonly decimal PrecoLimiteSuperiorExclusivo =
        Potencia10(PrecoPrecisao - PrecoEscala);

    private static decimal Potencia10(int expoente)
    {
        var resultado = 1m;
        for (var i = 0; i < expoente; i++)
        {
            resultado *= 10m;
        }

        return resultado;
    }
}

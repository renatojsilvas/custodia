namespace Custodia.Domain.Movimentos;

public static class InstrumentosCaixa
{
    public const string Prefixo = "caixa:";

    public const string Brl = "caixa:BRL";

    public const string ALiquidar = "caixa:a_liquidar";

    public static IReadOnlyCollection<string> Todos { get; } = [Brl, ALiquidar];
}

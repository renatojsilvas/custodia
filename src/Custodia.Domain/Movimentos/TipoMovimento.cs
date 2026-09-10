using Custodia.Domain.Common;

namespace Custodia.Domain.Movimentos;

public sealed record TipoMovimento
{
    public static readonly TipoMovimento Compra = new("compra");
    public static readonly TipoMovimento Venda = new("venda");
    public static readonly TipoMovimento Aporte = new("aporte");
    public static readonly TipoMovimento Cupom = new("cupom");
    public static readonly TipoMovimento Resgate = new("resgate");
    public static readonly TipoMovimento IrRetido = new("ir_retido");
    public static readonly TipoMovimento Iof = new("iof");
    public static readonly TipoMovimento ALiquidar = new("a_liquidar");
    public static readonly TipoMovimento Liquidacao = new("liquidacao");
    public static readonly TipoMovimento Ajuste = new("ajuste");

    public static IReadOnlyCollection<TipoMovimento> All { get; } =
        [Compra, Venda, Aporte, Cupom, Resgate, IrRetido, Iof, ALiquidar, Liquidacao, Ajuste];

    private TipoMovimento(string name) => Name = name;

    public string Name { get; }

    public static Result<TipoMovimento> FromName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var match = All.FirstOrDefault(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return match is not null
            ? match
            : MovimentoErrors.TipoInvalido;
    }
}

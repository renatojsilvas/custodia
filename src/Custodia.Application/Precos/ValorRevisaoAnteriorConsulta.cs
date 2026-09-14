namespace Custodia.Application.Precos;

public sealed record ValorRevisaoAnteriorConsulta(bool Existe, decimal Valor)
{
    public static readonly ValorRevisaoAnteriorConsulta Inexistente = new(false, default);

    public static ValorRevisaoAnteriorConsulta De(decimal valor) => new(true, valor);

    public decimal? ComoNullable() => Existe ? Valor : null;
}

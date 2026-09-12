namespace Custodia.Application.Movimentos;

public sealed record MaxDataEventoConsulta(bool Existe, DateOnly Valor)
{
    public static readonly MaxDataEventoConsulta Inexistente = new(false, default);

    public static MaxDataEventoConsulta De(DateOnly valor) => new(true, valor);

    public DateOnly? ComoNullable() => Existe ? Valor : null;
}

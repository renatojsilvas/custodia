namespace Custodia.Application.Calendario;

public sealed record ProximoDiaUtilConsulta(bool Encontrado, DateOnly Valor)
{
    public static readonly ProximoDiaUtilConsulta NaoEncontrado = new(false, default);

    public static ProximoDiaUtilConsulta De(DateOnly valor) => new(true, valor);
}

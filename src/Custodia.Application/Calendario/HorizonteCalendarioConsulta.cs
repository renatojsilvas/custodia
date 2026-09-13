namespace Custodia.Application.Calendario;

public sealed record HorizonteCalendarioConsulta(DateOnly? DataMaxima, DateOnly Hoje)
{
    public int DiasRestantes() => DataMaxima.HasValue ? DataMaxima.Value.DayNumber - Hoje.DayNumber : int.MinValue;
}

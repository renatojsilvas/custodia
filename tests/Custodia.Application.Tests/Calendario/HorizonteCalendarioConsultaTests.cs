using Custodia.Application.Calendario;

namespace Custodia.Application.Tests.Calendario;

public sealed class HorizonteCalendarioConsultaTests
{
    [Fact]
    public void DiasRestantes_ComDataMaximaAposHoje_DevolveADiferencaEmDiasCorridos()
    {
        var consulta = new HorizonteCalendarioConsulta(new DateOnly(2030, 12, 31), new DateOnly(2026, 9, 12));

        Assert.Equal(new DateOnly(2030, 12, 31).DayNumber - new DateOnly(2026, 9, 12).DayNumber, consulta.DiasRestantes());
    }

    [Fact]
    public void DiasRestantes_SemDataMaxima_DevolveSentinelaNegativaExtremaEmVezDeZero()
    {
        var consulta = new HorizonteCalendarioConsulta(null, new DateOnly(2026, 9, 12));

        Assert.Equal(int.MinValue, consulta.DiasRestantes());
    }
}

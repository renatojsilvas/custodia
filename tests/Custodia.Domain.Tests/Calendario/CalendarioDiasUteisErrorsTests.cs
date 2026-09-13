using Custodia.Domain.Calendario;
using Custodia.Domain.Common;

namespace Custodia.Domain.Tests.Calendario;

public sealed class CalendarioDiasUteisErrorsTests
{
    [Fact]
    public void HorizonteEsgotado_EhFalhaDeTipoUnavailable_NuncaModoDegradado()
    {
        Assert.Equal(ErrorType.Unavailable, CalendarioDiasUteisErrors.HorizonteEsgotado.Type);
    }

    [Fact]
    public void HorizonteEsgotado_TemCodigoEstavel()
    {
        Assert.Equal("CalendarioDiasUteis.HorizonteEsgotado", CalendarioDiasUteisErrors.HorizonteEsgotado.Code);
    }
}

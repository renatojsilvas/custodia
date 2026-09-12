using System.Globalization;

namespace Custodia.Application.Eventos;

internal static class DecimalContrato
{
    private const NumberStyles Estilo = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    public static bool TryParse(string? valor, out decimal resultado) =>
        decimal.TryParse(valor, Estilo, CultureInfo.InvariantCulture, out resultado);
}

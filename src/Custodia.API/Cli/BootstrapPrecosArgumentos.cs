using System.Globalization;

namespace Custodia.API.Cli;

public sealed record BootstrapPrecosArgumentosResultado(DateOnly? Desde, DateOnly? Ate, string? ErroDeUso)
{
    public bool EhValido => ErroDeUso is null;

    public static BootstrapPrecosArgumentosResultado Sucesso(DateOnly? desde, DateOnly? ate) => new(desde, ate, null);

    public static BootstrapPrecosArgumentosResultado Uso(string mensagem) => new(null, null, mensagem);
}

public static class BootstrapPrecosArgumentos
{
    public const string ArgumentoDesde = "--desde";
    public const string ArgumentoAte = "--ate";
    public const string FormatoData = "yyyy-MM-dd";

    public static BootstrapPrecosArgumentosResultado Interpretar(IReadOnlyList<string> args)
    {
        DateOnly? desde = null;
        DateOnly? ate = null;

        var i = 1;
        while (i < args.Count)
        {
            var argumento = args[i];

            if (argumento != ArgumentoDesde && argumento != ArgumentoAte)
            {
                return BootstrapPrecosArgumentosResultado.Uso($"Argumento desconhecido: '{argumento}'.");
            }

            if (i + 1 >= args.Count)
            {
                return BootstrapPrecosArgumentosResultado.Uso(
                    $"{argumento} exige um valor no formato {FormatoData}.");
            }

            var valorBruto = args[i + 1];
            if (!DateOnly.TryParseExact(
                    valorBruto, FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                return BootstrapPrecosArgumentosResultado.Uso(
                    $"{argumento} '{valorBruto}' não está no formato {FormatoData}.");
            }

            if (argumento == ArgumentoDesde)
            {
                desde = data;
            }
            else
            {
                ate = data;
            }

            i += 2;
        }

        return BootstrapPrecosArgumentosResultado.Sucesso(desde, ate);
    }
}

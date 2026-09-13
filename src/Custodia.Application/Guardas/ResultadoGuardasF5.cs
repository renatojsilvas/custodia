namespace Custodia.Application.Guardas;

public sealed record ResultadoGuardasF5(
    int ResgatesSemAliq,
    int AjustesDeResgateSemReversao,
    int ALiquidarVencidaSemLiquidacao,
    int TributosDivergentesDoRederivado);

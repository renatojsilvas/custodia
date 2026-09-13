namespace Custodia.Application.Conciliacao;

public sealed record ResultadoConciliacaoDeResgates(
    int ResgatesSemAliq,
    int AjustesDeResgateSemReversao,
    int ALiquidarVencidaSemLiquidacao,
    int TributosDivergentesDoRederivado);

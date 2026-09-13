namespace Custodia.Application.Reparo;

public sealed record RepararResgatesAntigosResultado(
    int ResgatesCandidatos,
    int ResgatesBackfilled,
    int AjustesCandidatos,
    int AjustesBackfilled);

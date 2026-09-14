namespace Custodia.Application.Precos.Hub;

public sealed record PrecoAsOfCampo(
    decimal Valor,
    string Fonte,
    int Revisao,
    DateTimeOffset ObservadoEm,
    DateOnly DataRef);

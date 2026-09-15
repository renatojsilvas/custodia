namespace Custodia.Application.Precos.Hub;

public sealed record PrecoAsOfItem(
    string InstrumentoId,
    DateOnly? DataRef,
    string? CampoPosicao,
    IReadOnlyDictionary<string, PrecoAsOfCampo>? Campos,
    PrecoAsOfMotivo Motivo);

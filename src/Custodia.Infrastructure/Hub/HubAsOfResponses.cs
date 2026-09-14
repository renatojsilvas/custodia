namespace Custodia.Infrastructure.Hub;

internal sealed record HubAsOfResponse(string? Date, List<HubAsOfItemResponse>? Items);

internal sealed record HubAsOfItemResponse(
    string? InstrumentoId,
    string? DataRef,
    string? CampoPosicao,
    Dictionary<string, HubAsOfCampoResponse?>? Campos,
    string? Motivo);

internal sealed record HubAsOfCampoResponse(string? Valor, string? Fonte, int Revisao, string? ObservadoEm, string? DataRef);

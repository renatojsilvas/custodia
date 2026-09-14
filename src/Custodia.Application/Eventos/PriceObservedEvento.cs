namespace Custodia.Application.Eventos;

public sealed record PriceObservedEvento(
    string InstrumentoId,
    DateOnly DataRef,
    string Campo,
    decimal Valor,
    string Fonte,
    int Revisao,
    DateTimeOffset ObservadoEm);

namespace Custodia.Application.Precos;

public sealed record ObservacaoDePreco(
    string InstrumentoId,
    DateOnly DataRef,
    string Campo,
    string Fonte,
    decimal Valor,
    int Revisao,
    DateTimeOffset ObservadoEm);

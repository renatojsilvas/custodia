namespace Custodia.Application.Eventos;

public sealed record TradeRegisteredEvento(
    string TradeId,
    string ClienteId,
    string InstrumentoId,
    OperacaoTrade Operacao,
    decimal Quantidade,
    decimal ValorFinanceiro,
    DateOnly DataEvento,
    DateTimeOffset RegistradoEm,
    string? EstornaTradeId,
    string? ValorOrigemSaldoBruto);

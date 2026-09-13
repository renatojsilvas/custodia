namespace Custodia.Application.Liquidacao;

public sealed record AReceberVencido(string ClienteId, string TradeId, DateOnly DataEvento);

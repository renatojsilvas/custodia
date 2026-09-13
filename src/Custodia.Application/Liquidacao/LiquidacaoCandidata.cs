namespace Custodia.Application.Liquidacao;

public sealed record LiquidacaoCandidata(string ClienteId, string TradeId, DateOnly DataEvento);

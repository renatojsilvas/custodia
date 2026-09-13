namespace Custodia.Domain.Posicoes;

public sealed record Lote(decimal Quantidade, decimal CustoUnitario, DateOnly DataAquisicao);

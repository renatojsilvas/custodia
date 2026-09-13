namespace Custodia.Domain.Posicoes;

public sealed record ConsumoDeFila(IReadOnlyList<LoteConsumido> Lotes, decimal QuantidadeDescoberta)
{
    public bool CoberturaCompleta => QuantidadeDescoberta == 0m;
}

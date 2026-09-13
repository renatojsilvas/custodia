namespace Custodia.Domain.Posicoes;

public sealed record ConsumoDeFila(IReadOnlyList<Lote> Lotes, decimal QuantidadeDescoberta)
{
    public bool CoberturaCompleta => QuantidadeDescoberta == 0m;
}

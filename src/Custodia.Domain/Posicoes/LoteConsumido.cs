namespace Custodia.Domain.Posicoes;

public sealed class LoteConsumido
{
    public LoteConsumido(decimal quantidade, decimal custoUnitario, DateOnly dataAquisicao, int prazo)
    {
        if (prazo < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prazo), prazo, "Lote consumido com aquisição posterior à data do resgate é inconstruível.");
        }

        Quantidade = quantidade;
        CustoUnitario = custoUnitario;
        DataAquisicao = dataAquisicao;
        Prazo = prazo;
    }

    public decimal Quantidade { get; }

    public decimal CustoUnitario { get; }

    public DateOnly DataAquisicao { get; }

    public int Prazo { get; }
}

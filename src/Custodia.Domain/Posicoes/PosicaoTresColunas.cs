namespace Custodia.Domain.Posicoes;

public sealed record PosicaoTresColunas(decimal Quantidade, decimal CustoTotal, decimal PrecoMedio)
{
    public static readonly PosicaoTresColunas Zero = new(0m, 0m, 0m);
}

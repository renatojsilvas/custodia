namespace Custodia.Domain.Posicoes;

public sealed class PosicaoCorrente
{
    private PosicaoCorrente()
    {
    }

    public string ClienteId { get; private set; } = string.Empty;

    public string InstrumentoId { get; private set; } = string.Empty;

    public decimal Quantidade { get; private set; }

    public decimal? PrecoMedio { get; private set; }

    public decimal? CustoTotal { get; private set; }
}

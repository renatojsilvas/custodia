namespace Custodia.Domain.Posicoes;

public sealed class SnapshotPosicao
{
    private SnapshotPosicao()
    {
    }

    public string ClienteId { get; private set; } = string.Empty;

    public string InstrumentoId { get; private set; } = string.Empty;

    public DateOnly Data { get; private set; }

    public decimal Quantidade { get; private set; }

    public decimal Preco { get; private set; }

    public decimal Valor { get; private set; }

    public decimal? PrecoMedio { get; private set; }

    public decimal? Custo { get; private set; }

    public DateTimeOffset CalculadoEm { get; private set; }

    public bool Vigente { get; private set; }
}

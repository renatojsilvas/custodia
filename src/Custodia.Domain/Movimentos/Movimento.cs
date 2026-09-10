namespace Custodia.Domain.Movimentos;

public sealed class Movimento
{
    private Movimento()
    {
    }

    public long Id { get; private set; }

    public string ClienteId { get; private set; } = string.Empty;

    public string InstrumentoId { get; private set; } = string.Empty;

    public TipoMovimento Tipo { get; private set; } = null!;

    public DateOnly DataEvento { get; private set; }

    public DateTimeOffset RegistradoEm { get; private set; }

    public decimal QtdDelta { get; private set; }

    public decimal ValorFinanceiro { get; private set; }

    public string RefExterna { get; private set; } = string.Empty;

    public long? RefEstorno { get; private set; }
}

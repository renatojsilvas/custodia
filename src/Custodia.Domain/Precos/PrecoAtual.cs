namespace Custodia.Domain.Precos;

public sealed class PrecoAtual
{
    private PrecoAtual()
    {
    }

    public string InstrumentoId { get; private set; } = string.Empty;

    public DateOnly DataRef { get; private set; }

    public string Campo { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    public int Revisao { get; private set; }
}

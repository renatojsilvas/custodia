namespace Custodia.Domain.Precos;

public sealed class HistoricoPreco
{
    private HistoricoPreco()
    {
    }

    public string InstrumentoId { get; private set; } = string.Empty;

    public DateOnly DataRef { get; private set; }

    public string Campo { get; private set; } = string.Empty;

    public string Fonte { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    public int Revisao { get; private set; }

    public DateTimeOffset ObservadoEm { get; private set; }
}

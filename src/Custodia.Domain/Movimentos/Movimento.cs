using Custodia.Domain.Common;

namespace Custodia.Domain.Movimentos;

public sealed class Movimento
{
    private Movimento()
    {
    }

    private Movimento(
        string clienteId,
        string instrumentoId,
        TipoMovimento tipo,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        decimal qtdDelta,
        decimal valorFinanceiro,
        string refExterna,
        long? refEstorno)
    {
        ClienteId = clienteId;
        InstrumentoId = instrumentoId;
        Tipo = tipo;
        DataEvento = dataEvento;
        RegistradoEm = registradoEm;
        QtdDelta = qtdDelta;
        ValorFinanceiro = valorFinanceiro;
        RefExterna = refExterna;
        RefEstorno = refEstorno;
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

    public static Result<Movimento> Create(
        string clienteId,
        string instrumentoId,
        TipoMovimento tipo,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        decimal qtdDelta,
        decimal valorFinanceiro,
        string refExterna,
        long? refEstorno = null)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        if (ExcedePrecisaoSuportada(qtdDelta, SchemaNumericLimits.QuantidadeLimiteSuperiorExclusivo, SchemaNumericLimits.QuantidadeEscala))
        {
            return MovimentoErrors.QtdDeltaExcedePrecisaoSuportada;
        }

        if (ExcedePrecisaoSuportada(valorFinanceiro, SchemaNumericLimits.ValorLimiteSuperiorExclusivo, SchemaNumericLimits.ValorEscala))
        {
            return MovimentoErrors.ValorFinanceiroExcedePrecisaoSuportada;
        }

        return new Movimento(clienteId, instrumentoId, tipo, dataEvento, registradoEm, qtdDelta, valorFinanceiro, refExterna, refEstorno);
    }

    private static bool ExcedePrecisaoSuportada(decimal valor, decimal limiteSuperiorExclusivo, int escala) =>
        Math.Abs(valor) >= limiteSuperiorExclusivo || decimal.Round(valor, escala) != valor;

    public static Movimento Reconstituir(
        long id,
        string clienteId,
        string instrumentoId,
        TipoMovimento tipo,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        decimal qtdDelta,
        decimal valorFinanceiro,
        string refExterna,
        long? refEstorno)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return new Movimento(clienteId, instrumentoId, tipo, dataEvento, registradoEm, qtdDelta, valorFinanceiro, refExterna, refEstorno)
        {
            Id = id,
        };
    }
}

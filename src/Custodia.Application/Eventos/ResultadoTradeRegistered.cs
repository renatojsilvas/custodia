using Custodia.Domain.Eventos;

namespace Custodia.Application.Eventos;

public enum ResultadoTradeRegisteredTipo
{
    Escriturado,
    Estacionar,
    EnviarParaRetry,
}

public sealed record ResultadoTradeRegistered
{
    private ResultadoTradeRegistered(ResultadoTradeRegisteredTipo tipo, MotivoParking? motivo, bool replay)
    {
        Tipo = tipo;
        Motivo = motivo;
        Replay = replay;
    }

    public ResultadoTradeRegisteredTipo Tipo { get; }

    public MotivoParking? Motivo { get; }

    public bool Replay { get; }

    public static ResultadoTradeRegistered Escriturado(bool replay = false) =>
        new(ResultadoTradeRegisteredTipo.Escriturado, null, replay);

    public static ResultadoTradeRegistered Estacionar(MotivoParking motivo) =>
        new(ResultadoTradeRegisteredTipo.Estacionar, motivo, false);

    public static ResultadoTradeRegistered EnviarParaRetry() =>
        new(ResultadoTradeRegisteredTipo.EnviarParaRetry, null, false);
}

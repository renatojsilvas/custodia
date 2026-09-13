using Custodia.Domain.Eventos;

namespace Custodia.Application.Eventos;

public enum DesfechoRoteamentoTipo
{
    Escriturar,
    Estacionar,
    Ignorar,
}

public sealed record DesfechoRoteamento
{
    private DesfechoRoteamento(DesfechoRoteamentoTipo tipo, TradeRegisteredEvento? evento, MotivoParking? motivo)
    {
        Tipo = tipo;
        Evento = evento;
        Motivo = motivo;
    }

    public DesfechoRoteamentoTipo Tipo { get; }

    public TradeRegisteredEvento? Evento { get; }

    public MotivoParking? Motivo { get; }

    public static DesfechoRoteamento Escriturar(TradeRegisteredEvento evento) =>
        new(DesfechoRoteamentoTipo.Escriturar, evento, null);

    public static DesfechoRoteamento Estacionar(MotivoParking motivo) =>
        new(DesfechoRoteamentoTipo.Estacionar, null, motivo);

    public static DesfechoRoteamento Ignorar() =>
        new(DesfechoRoteamentoTipo.Ignorar, null, null);
}

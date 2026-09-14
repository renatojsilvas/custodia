using Custodia.Domain.Eventos;

namespace Custodia.Application.Eventos;

public enum DesfechoRoteamentoTipo
{
    Escriturar,
    Observar,
    Estacionar,
    Ignorar,
}

public sealed record DesfechoRoteamento
{
    private DesfechoRoteamento(
        DesfechoRoteamentoTipo tipo, TradeRegisteredEvento? evento, PriceObservedEvento? eventoPreco, MotivoParking? motivo)
    {
        Tipo = tipo;
        Evento = evento;
        EventoPreco = eventoPreco;
        Motivo = motivo;
    }

    public DesfechoRoteamentoTipo Tipo { get; }

    public TradeRegisteredEvento? Evento { get; }

    public PriceObservedEvento? EventoPreco { get; }

    public MotivoParking? Motivo { get; }

    public static DesfechoRoteamento Escriturar(TradeRegisteredEvento evento) =>
        new(DesfechoRoteamentoTipo.Escriturar, evento, null, null);

    public static DesfechoRoteamento Observar(PriceObservedEvento evento) =>
        new(DesfechoRoteamentoTipo.Observar, null, evento, null);

    public static DesfechoRoteamento Estacionar(MotivoParking motivo) =>
        new(DesfechoRoteamentoTipo.Estacionar, null, null, motivo);

    public static DesfechoRoteamento Ignorar() =>
        new(DesfechoRoteamentoTipo.Ignorar, null, null, null);
}

using Custodia.Domain.Eventos;

namespace Custodia.Infrastructure.Messaging;

public enum ResultadoReprocessamentoTipo
{
    Processado,
    NaoProcessado,
    Ignorado,
}

public sealed record ResultadoReprocessamento(ResultadoReprocessamentoTipo Tipo, MotivoEstacionamento? MotivoResultante)
{
    public static ResultadoReprocessamento Sucesso() => new(ResultadoReprocessamentoTipo.Processado, null);

    public static ResultadoReprocessamento Ignorar() => new(ResultadoReprocessamentoTipo.Ignorado, null);

    public static ResultadoReprocessamento Falha(MotivoEstacionamento motivo) =>
        new(ResultadoReprocessamentoTipo.NaoProcessado, motivo);
}

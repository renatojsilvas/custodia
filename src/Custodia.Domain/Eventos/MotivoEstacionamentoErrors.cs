using Custodia.Domain.Common;

namespace Custodia.Domain.Eventos;

public static class MotivoEstacionamentoErrors
{
    public static readonly Error MotivoInvalido =
        new("MotivoEstacionamento.Invalido", "Motivo de estacionamento inválido.", ErrorType.Unprocessable);
}

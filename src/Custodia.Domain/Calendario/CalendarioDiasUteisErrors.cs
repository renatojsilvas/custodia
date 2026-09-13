using Custodia.Domain.Common;

namespace Custodia.Domain.Calendario;

public static class CalendarioDiasUteisErrors
{
    public static readonly Error HorizonteEsgotado =
        new(
            "CalendarioDiasUteis.HorizonteEsgotado",
            "Não existe dia útil semeado após a data informada dentro do horizonte do calendário. " +
            "Semeie mais datas por migration antes que o processamento dependente pare.",
            ErrorType.Unavailable);
}

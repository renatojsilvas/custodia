using Custodia.Domain.Common;

namespace Custodia.Application.Precos.Bootstrap;

public static class ColetaDePrecosErrors
{
    public static readonly Error DesdeMaiorQueAte = new(
        "ColetaDePrecos.DesdeMaiorQueAte",
        "O parâmetro --desde não pode ser posterior a --ate.",
        ErrorType.Validation);

    public static readonly Error TetoDeDiasExcedido = new(
        "Hub.ColetaIncompleta",
        "A janela de datas [desde, ate] excede o teto configurado em Decisao:BootstrapPrecosTetoDias; " +
        "fatie a coleta com --desde/--ate.",
        ErrorType.Unavailable);

    public static readonly Error TetoDeFatiasExcedido = new(
        "Hub.ColetaIncompleta",
        "O número de fatias de instrumentos excede o teto configurado em Decisao:BootstrapPrecosTetoFatias.",
        ErrorType.Unavailable);
}

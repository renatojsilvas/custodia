namespace Custodia.Domain.Tributos;

public static class TabelaTributosResgate
{
    private static readonly decimal[] AliquotasIofPorDiaCorrido =
    [
        96m, 93m, 90m, 86m, 83m, 80m, 76m, 73m, 70m, 66m,
        63m, 60m, 56m, 53m, 50m, 46m, 43m, 40m, 36m, 33m,
        30m, 26m, 23m, 20m, 16m, 13m, 10m, 6m, 3m,
    ];

    private static readonly (int DiasMin, int DiasMax, decimal Aliquota)[] FaixasIrPorDiasCorridos =
    [
        (0, 180, 22.5m),
        (181, 360, 20m),
        (361, 720, 17.5m),
        (721, 999_999, 15m),
    ];

    public static decimal? AliquotaIof(int diasCorridos) =>
        diasCorridos >= 1 && diasCorridos <= AliquotasIofPorDiaCorrido.Length
            ? AliquotasIofPorDiaCorrido[diasCorridos - 1]
            : null;

    public static decimal AliquotaIr(int diasCorridos) =>
        FaixasIrPorDiasCorridos
            .First(faixa => diasCorridos >= faixa.DiasMin && diasCorridos <= faixa.DiasMax)
            .Aliquota;
}

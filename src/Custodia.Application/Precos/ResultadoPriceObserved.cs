namespace Custodia.Application.Precos;

public enum ResultadoPriceObservedTipo
{
    AplicadoEmPrecoAtual,
    SoHistorico,
    ReplaySemAlteracao,
    ValorDivergente,
}

public sealed record ResultadoPriceObserved(
    ResultadoPriceObservedTipo Tipo,
    MotivoPrecoAtualNaoAtualizado? MotivoSoHistorico,
    decimal? ValorAnteriorDivergente,
    decimal? ValorNovoDivergente)
{
    public static ResultadoPriceObserved AplicadoEmPrecoAtual() =>
        new(ResultadoPriceObservedTipo.AplicadoEmPrecoAtual, null, null, null);

    public static ResultadoPriceObserved SoHistorico(MotivoPrecoAtualNaoAtualizado motivo) =>
        new(ResultadoPriceObservedTipo.SoHistorico, motivo, null, null);

    public static ResultadoPriceObserved ReplaySemAlteracao() =>
        new(ResultadoPriceObservedTipo.ReplaySemAlteracao, null, null, null);

    public static ResultadoPriceObserved ValorDivergente(decimal valorAnterior, decimal valorNovo) =>
        new(ResultadoPriceObservedTipo.ValorDivergente, null, valorAnterior, valorNovo);
}

namespace Custodia.Application.Precos;

public enum ResultadoHistoricoTipo
{
    Inserido,
    ReplayInocuo,
    Divergente,
}

public sealed record ResultadoHistorico(ResultadoHistoricoTipo Tipo, decimal? ValorAnterior)
{
    public static ResultadoHistorico Inserido() => new(ResultadoHistoricoTipo.Inserido, null);

    public static ResultadoHistorico ReplayInocuo() => new(ResultadoHistoricoTipo.ReplayInocuo, null);

    public static ResultadoHistorico Divergente(decimal valorAnterior) =>
        new(ResultadoHistoricoTipo.Divergente, valorAnterior);
}

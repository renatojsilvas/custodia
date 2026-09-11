using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Tests.Movimentos;

public static class MovimentoTestExtensions
{
    public static Movimento ComId(this Movimento movimento, long id) =>
        Movimento.Reconstituir(
            id,
            movimento.ClienteId,
            movimento.InstrumentoId,
            movimento.Tipo,
            movimento.DataEvento,
            movimento.RegistradoEm,
            movimento.QtdDelta,
            movimento.ValorFinanceiro,
            movimento.RefExterna,
            movimento.RefEstorno);

    public static Movimento MovimentoValido(
        long id,
        string clienteId,
        string instrumentoId,
        TipoMovimento tipo,
        DateOnly dataEvento,
        DateTimeOffset registradoEm,
        decimal qtdDelta,
        decimal valorFinanceiro,
        string refExterna,
        long? refEstorno = null) =>
        Movimento.Create(clienteId, instrumentoId, tipo, dataEvento, registradoEm, qtdDelta, valorFinanceiro, refExterna, refEstorno)
            .Value
            .ComId(id);
}

using Custodia.Domain.Common;

namespace Custodia.Domain.Movimentos;

public static class AjusteDeReversao
{
    public static Result<Movimento> Criar(
        Movimento revertida, string clienteId, DateTimeOffset registradoEm, string refExterna) =>
        Movimento.Create(
            clienteId,
            revertida.InstrumentoId,
            TipoMovimento.Ajuste,
            revertida.DataEvento,
            registradoEm,
            qtdDelta: -revertida.QtdDelta,
            valorFinanceiro: -revertida.ValorFinanceiro,
            refExterna: refExterna,
            refEstorno: revertida.Id);
}

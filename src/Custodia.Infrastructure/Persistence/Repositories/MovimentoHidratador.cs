using Custodia.Domain.Movimentos;

namespace Custodia.Infrastructure.Persistence.Repositories;

internal sealed record MovimentoRow(
    long Id,
    string ClienteId,
    string InstrumentoId,
    string Tipo,
    DateOnly DataEvento,
    DateTimeOffset RegistradoEm,
    decimal QtdDelta,
    decimal ValorFinanceiro,
    string RefExterna,
    long? RefEstorno);

internal static class MovimentoHidratador
{
    public static Movimento Hidratar(MovimentoRow row)
    {
        var tipo = TipoMovimento.FromName(row.Tipo).Value;

        return Movimento.Reconstituir(
            row.Id,
            row.ClienteId,
            row.InstrumentoId,
            tipo,
            row.DataEvento,
            row.RegistradoEm,
            row.QtdDelta,
            row.ValorFinanceiro,
            row.RefExterna,
            row.RefEstorno);
    }
}

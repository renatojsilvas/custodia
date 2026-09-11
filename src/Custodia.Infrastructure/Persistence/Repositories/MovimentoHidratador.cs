using System.Reflection;
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
    private static readonly ConstructorInfo Construtor = typeof(Movimento).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        binder: null,
        types:
        [
            typeof(string), typeof(string), typeof(TipoMovimento), typeof(DateOnly), typeof(DateTimeOffset),
            typeof(decimal), typeof(decimal), typeof(string), typeof(long?),
        ],
        modifiers: null)
        ?? throw new InvalidOperationException("Movimento: construtor de reidratação não encontrado por reflexão.");

    private static readonly PropertyInfo IdProperty = typeof(Movimento).GetProperty(nameof(Movimento.Id))
        ?? throw new InvalidOperationException("Movimento.Id não encontrado por reflexão.");

    public static Movimento Hidratar(MovimentoRow row)
    {
        var tipo = TipoMovimento.FromName(row.Tipo).Value;

        var movimento = (Movimento)Construtor.Invoke(
        [
            row.ClienteId,
            row.InstrumentoId,
            tipo,
            row.DataEvento,
            row.RegistradoEm,
            row.QtdDelta,
            row.ValorFinanceiro,
            row.RefExterna,
            row.RefEstorno,
        ]);

        IdProperty.SetValue(movimento, row.Id);

        return movimento;
    }
}

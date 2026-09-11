using System.Reflection;
using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Tests.Movimentos;

public static class MovimentoTestExtensions
{
    private static readonly PropertyInfo IdProperty =
        typeof(Movimento).GetProperty(nameof(Movimento.Id))
        ?? throw new InvalidOperationException("Movimento.Id não encontrado por reflexão.");

    public static Movimento ComId(this Movimento movimento, long id)
    {
        IdProperty.SetValue(movimento, id);
        return movimento;
    }

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

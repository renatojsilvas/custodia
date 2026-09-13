using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Posicoes;

public static class SequenciaCanonica
{
    public static IReadOnlyList<Movimento> MovimentosEfetivos(IEnumerable<Movimento> movimentos, DateOnly? corte = null)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        var candidatos = corte is null
            ? movimentos.ToList()
            : movimentos.Where(movimento => movimento.DataEvento <= corte.Value).ToList();

        var ordenados = candidatos
            .OrderBy(movimento => movimento.DataEvento)
            .ThenBy(movimento => movimento.RegistradoEm)
            .ThenBy(movimento => movimento.Id)
            .ToList();

        var reversoresPorAlvoId = ordenados
            .Where(movimento => movimento.Tipo == TipoMovimento.Ajuste && movimento.RefEstorno is not null)
            .ToLookup(movimento => movimento.RefEstorno!.Value, movimento => movimento);

        return ordenados
            .Where(movimento => movimento.Tipo != TipoMovimento.Ajuste)
            .Where(movimento => EhEfetiva(movimento, reversoresPorAlvoId, []))
            .ToList();
    }

    private static bool EhEfetiva(Movimento movimento, ILookup<long, Movimento> reversoresPorAlvoId, HashSet<long> emResolucao)
    {
        var reversores = reversoresPorAlvoId[movimento.Id];

        if (!reversores.Any())
        {
            return true;
        }

        if (!emResolucao.Add(movimento.Id))
        {
            return true;
        }

        var existeReversorEfetivo = reversores.Any(reversor => EhEfetiva(reversor, reversoresPorAlvoId, emResolucao));
        emResolucao.Remove(movimento.Id);
        return !existeReversorEfetivo;
    }
}

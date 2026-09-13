using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Posicoes;

public static class LivroSemEstornos
{
    public static IReadOnlyList<Movimento> MovimentosEfetivos(IEnumerable<Movimento> movimentos, CortePosicional? corte = null)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        var ordenados = movimentos
            .OrderBy(movimento => movimento.DataEvento)
            .ThenBy(movimento => movimento.RegistradoEm)
            .ThenBy(movimento => movimento.Id)
            .ToList();

        var reversoresPorAlvoId = ordenados
            .Where(movimento => movimento.Tipo == TipoMovimento.Ajuste && movimento.RefEstorno is not null)
            .ToLookup(movimento => movimento.RefEstorno!.Value, movimento => movimento);

        var efetivos = ordenados
            .Where(movimento => movimento.Tipo != TipoMovimento.Ajuste)
            .Where(movimento => EhEfetiva(movimento, reversoresPorAlvoId, []));

        return corte is null
            ? efetivos.ToList()
            : efetivos.Where(movimento => corte.Value.Inclui(movimento)).ToList();
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

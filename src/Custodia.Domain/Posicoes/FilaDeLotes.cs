using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Posicoes;

public static class FilaDeLotes
{
    public static IReadOnlyList<Lote> Reconstruir(IEnumerable<Movimento> movimentos, DateOnly? corte = null)
    {
        var efetivos = SequenciaCanonica.MovimentosEfetivos(movimentos, corte);

        var fila = new LinkedList<Lote>();

        foreach (var movimento in efetivos)
        {
            if (InstrumentosCaixa.Todos.Contains(movimento.InstrumentoId))
            {
                continue;
            }

            if (FormaLote(movimento.Tipo))
            {
                if (movimento.QtdDelta > 0m)
                {
                    var custoUnitario = movimento.ValorFinanceiro / movimento.QtdDelta;
                    fila.AddLast(new Lote(movimento.QtdDelta, custoUnitario, movimento.DataEvento));
                }

                continue;
            }

            if (movimento.Tipo == TipoMovimento.Venda || movimento.Tipo == TipoMovimento.Resgate)
            {
                Consumir(fila, Math.Abs(movimento.QtdDelta));
            }
        }

        return fila.ToList();
    }

    public static ConsumoDeFila ConsumirParaResgate(
        IReadOnlyList<Lote> filaAntesDoResgate, decimal quantidadeResgate, DateOnly dataResgate)
    {
        ArgumentNullException.ThrowIfNull(filaAntesDoResgate);

        var fila = new LinkedList<Lote>(filaAntesDoResgate);
        var consumos = new List<Lote>();
        var restante = quantidadeResgate;

        while (restante > 0m && fila.Count > 0)
        {
            var primeiro = fila.First!.Value;

            if (primeiro.Quantidade <= restante)
            {
                consumos.Add(primeiro);
                restante -= primeiro.Quantidade;
                fila.RemoveFirst();
            }
            else
            {
                consumos.Add(primeiro with { Quantidade = restante });
                fila.First!.Value = primeiro with { Quantidade = primeiro.Quantidade - restante };
                restante = 0m;
            }
        }

        if (restante > 0m)
        {
            var ultimoLoteDaFilaOriginal = filaAntesDoResgate.Count > 0 ? filaAntesDoResgate[^1] : null;

            consumos.Add(new Lote(
                restante,
                ultimoLoteDaFilaOriginal?.CustoUnitario ?? 0m,
                ultimoLoteDaFilaOriginal?.DataAquisicao ?? dataResgate));
        }

        return new ConsumoDeFila(consumos, restante);
    }

    private static bool FormaLote(TipoMovimento tipo) =>
        tipo == TipoMovimento.Compra || tipo == TipoMovimento.Aporte;

    private static void Consumir(LinkedList<Lote> fila, decimal quantidade)
    {
        var restante = quantidade;

        while (restante > 0m && fila.Count > 0)
        {
            var primeiro = fila.First!.Value;

            if (primeiro.Quantidade <= restante)
            {
                restante -= primeiro.Quantidade;
                fila.RemoveFirst();
            }
            else
            {
                fila.First!.Value = primeiro with { Quantidade = primeiro.Quantidade - restante };
                restante = 0m;
            }
        }
    }
}

using Custodia.Domain.Posicoes;

namespace Custodia.Domain.Tributos;

public static class MotorTributosResgate
{
    public static ResultadoTributosResgate Calcular(
        IReadOnlyList<Lote> lotesConsumidos, decimal valorFinanceiroResgate, DateOnly dataResgate)
    {
        ArgumentNullException.ThrowIfNull(lotesConsumidos);

        if (lotesConsumidos.Count == 0)
        {
            return new ResultadoTributosResgate(0m, 0m);
        }

        var quantidadeTotal = lotesConsumidos.Sum(lote => lote.Quantidade);
        var rateios = Ratear(valorFinanceiroResgate, lotesConsumidos, quantidadeTotal);

        var iofTotal = 0m;
        var irTotal = 0m;

        for (var i = 0; i < lotesConsumidos.Count; i++)
        {
            var lote = lotesConsumidos[i];
            var prazo = dataResgate.DayNumber - lote.DataAquisicao.DayNumber;
            var custoDoLote = Math.Round(lote.CustoUnitario * lote.Quantidade, 2);
            var baseDoLote = Math.Max(rateios[i] - custoDoLote, 0m);

            var aliquotaIof = TabelaTributosResgate.AliquotaIof(prazo);
            var iofDoLote = aliquotaIof is null ? 0m : Math.Round(baseDoLote * aliquotaIof.Value / 100m, 2);

            var baseIrDoLote = baseDoLote - iofDoLote;
            var aliquotaIr = TabelaTributosResgate.AliquotaIr(prazo);
            var irDoLote = Math.Round(baseIrDoLote * aliquotaIr / 100m, 2);

            iofTotal += iofDoLote;
            irTotal += irDoLote;
        }

        return new ResultadoTributosResgate(iofTotal, irTotal);
    }

    private static IReadOnlyList<decimal> Ratear(decimal valorTotal, IReadOnlyList<Lote> lotes, decimal quantidadeTotal)
    {
        var rateios = new decimal[lotes.Count];
        var acumulado = 0m;

        for (var i = 0; i < lotes.Count - 1; i++)
        {
            var parcela = Math.Round(valorTotal * lotes[i].Quantidade / quantidadeTotal, 2);
            rateios[i] = parcela;
            acumulado += parcela;
        }

        rateios[^1] = valorTotal - acumulado;

        return rateios;
    }
}

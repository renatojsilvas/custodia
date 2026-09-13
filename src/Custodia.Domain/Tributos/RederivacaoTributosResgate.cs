using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Domain.Tributos;

public sealed record RederivacaoDeResgate(ConsumoDeFila Consumo, ResultadoTributosResgate Tributos);

public static class RederivacaoTributosResgate
{
    public static RederivacaoDeResgate Rederivar(IReadOnlyList<Movimento> movimentosDaChave, Movimento resgate)
    {
        ArgumentNullException.ThrowIfNull(movimentosDaChave);
        ArgumentNullException.ThrowIfNull(resgate);

        var corte = new CortePosicional(resgate.DataEvento, resgate.RegistradoEm);
        var movimentosAntesDoResgate = movimentosDaChave.Where(m => m.Id != resgate.Id).ToList();
        var fila = FilaDeLotes.Reconstruir(movimentosAntesDoResgate, corte);
        var consumo = FilaDeLotes.ConsumirParaResgate(fila, Math.Abs(resgate.QtdDelta), corte);
        var tributos = MotorTributosResgate.Calcular(consumo.Lotes, resgate.ValorFinanceiro);

        return new RederivacaoDeResgate(consumo, tributos);
    }
}

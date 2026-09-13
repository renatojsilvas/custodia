using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Domain.Tributos;

public sealed record RecalculoDeTributosDeResgate(ConsumoDeFila Consumo, ResultadoTributosResgate Tributos);

public static class RecalculoDeTributos
{
    public static RecalculoDeTributosDeResgate Rederivar(IReadOnlyList<Movimento> movimentosDaChave, Movimento resgate)
    {
        ArgumentNullException.ThrowIfNull(movimentosDaChave);
        ArgumentNullException.ThrowIfNull(resgate);

        var corte = new MomentoDoLivro(resgate.DataEvento, resgate.RegistradoEm);
        var movimentosAntesDoResgate = movimentosDaChave.Where(m => m.Id != resgate.Id).ToList();
        var fila = FilaDeLotes.Reconstruir(movimentosAntesDoResgate, corte);
        var consumo = FilaDeLotes.ConsumirParaResgate(fila, Math.Abs(resgate.QtdDelta), corte);
        var tributos = MotorTributosResgate.Calcular(consumo.Lotes, resgate.ValorFinanceiro);

        return new RecalculoDeTributosDeResgate(consumo, tributos);
    }
}

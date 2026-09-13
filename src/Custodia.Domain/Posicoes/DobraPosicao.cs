using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Posicoes;

public static class DobraPosicao
{
    private const decimal PrecoFixoPorDefinicao = 1.000000m;

    public static PosicaoTresColunas Dobrar(IEnumerable<Movimento> movimentos, DateOnly? corte = null)
    {
        var efetivos = SequenciaCanonica.MovimentosEfetivos(movimentos, corte);

        var estado = PosicaoTresColunas.Zero;

        foreach (var movimento in efetivos)
        {
            estado = AplicarMovimento(estado, movimento);
        }

        return estado;
    }

    public static ResultadoAplicacaoIncremental AplicarIncremental(
        PosicaoTresColunas estadoAtual,
        Movimento movimento,
        DateOnly? maxDataEventoDaChave)
    {
        ArgumentNullException.ThrowIfNull(estadoAtual);
        ArgumentNullException.ThrowIfNull(movimento);

        if (movimento.Tipo == TipoMovimento.Ajuste)
        {
            return ResultadoAplicacaoIncremental.ExigeRedobra();
        }

        var primeiraLinhaDaChave = maxDataEventoDaChave is null;
        var ehUltimaDaOrdem = primeiraLinhaDaChave || movimento.DataEvento >= maxDataEventoDaChave!.Value;

        if (!ehUltimaDaOrdem)
        {
            return ResultadoAplicacaoIncremental.ExigeRedobra();
        }

        return ResultadoAplicacaoIncremental.Aplicado(AplicarMovimento(estadoAtual, movimento));
    }

    private static PosicaoTresColunas AplicarMovimento(PosicaoTresColunas estadoAnterior, Movimento movimento)
    {
        var quantidadeResultante = estadoAnterior.Quantidade + movimento.QtdDelta;

        if (quantidadeResultante == 0m)
        {
            return PosicaoTresColunas.Zero;
        }

        if (InstrumentosCaixa.Todos.Contains(movimento.InstrumentoId))
        {
            return new PosicaoTresColunas(quantidadeResultante, quantidadeResultante, PrecoFixoPorDefinicao);
        }

        if (quantidadeResultante < 0m)
        {
            var custoTotalNegativo = CustoTotalPorTipo(movimento.Tipo, estadoAnterior, movimento, quantidadeResultante);
            return new PosicaoTresColunas(quantidadeResultante, custoTotalNegativo, estadoAnterior.PrecoMedio);
        }

        var cruzaDeNaoPositivoParaPositivo = estadoAnterior.Quantidade <= 0m
            && (movimento.Tipo == TipoMovimento.Compra || movimento.Tipo == TipoMovimento.Aporte);

        if (cruzaDeNaoPositivoParaPositivo)
        {
            var precoUnitarioDaLinha = movimento.ValorFinanceiro / movimento.QtdDelta;
            return new PosicaoTresColunas(quantidadeResultante, precoUnitarioDaLinha * quantidadeResultante, precoUnitarioDaLinha);
        }

        var custoTotal = CustoTotalPorTipo(movimento.Tipo, estadoAnterior, movimento, quantidadeResultante);
        var precoMedio = PrecoMedioPorTipo(movimento.Tipo, estadoAnterior, custoTotal, quantidadeResultante);
        return new PosicaoTresColunas(quantidadeResultante, custoTotal, precoMedio);
    }

    private static decimal CustoTotalPorTipo(
        TipoMovimento tipo,
        PosicaoTresColunas estadoAnterior,
        Movimento movimento,
        decimal quantidadeResultante)
    {
        if (tipo == TipoMovimento.Compra || tipo == TipoMovimento.Aporte)
        {
            return estadoAnterior.CustoTotal + movimento.ValorFinanceiro;
        }

        if (tipo == TipoMovimento.Venda || tipo == TipoMovimento.Resgate)
        {
            return estadoAnterior.PrecoMedio * quantidadeResultante;
        }

        if (tipo == TipoMovimento.Cupom)
        {
            return estadoAnterior.CustoTotal;
        }

        return quantidadeResultante;
    }

    private static decimal PrecoMedioPorTipo(
        TipoMovimento tipo,
        PosicaoTresColunas estadoAnterior,
        decimal custoTotal,
        decimal quantidadeResultante)
    {
        if (tipo == TipoMovimento.Compra || tipo == TipoMovimento.Aporte)
        {
            return custoTotal / quantidadeResultante;
        }

        if (tipo == TipoMovimento.Venda || tipo == TipoMovimento.Resgate || tipo == TipoMovimento.Cupom)
        {
            return estadoAnterior.PrecoMedio;
        }

        return PrecoFixoPorDefinicao;
    }
}

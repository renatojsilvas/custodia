using MediatR;
using Custodia.Application.Reparo;
using Custodia.Application.Calendario;
using Custodia.Application.Liquidacao;
using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Tributos;

namespace Custodia.Application.Conciliacao;

public sealed class ExecutarConciliacaoDeResgatesCommandHandler(
    IRepararResgatesAntigosReadRepository backfillReadRepository,
    IAReceberVencidoReadRepository liquidacaoCandidataReadRepository,
    IConciliacaoDeResgatesReadRepository guardasF5ReadRepository,
    IMovimentoReadRepository movimentoReadRepository,
    IProximoDiaUtilService proximoDiaUtilService,
    ICalendarioDiasUteisReadRepository calendarioDiasUteisReadRepository)
    : IRequestHandler<ExecutarConciliacaoDeResgatesCommand, Result<ResultadoConciliacaoDeResgates>>
{
    public async Task<Result<ResultadoConciliacaoDeResgates>> Handle(ExecutarConciliacaoDeResgatesCommand request, CancellationToken ct)
    {
        var resgatesSemAliqResult = await backfillReadRepository.ObterResgatesSemAliqAsync(ct);

        if (resgatesSemAliqResult.IsFailure)
        {
            return resgatesSemAliqResult.Error;
        }

        var ajustesSemReversaoResult = await backfillReadRepository.ObterAjustesDeResgateSemReversaoAsync(ct);

        if (ajustesSemReversaoResult.IsFailure)
        {
            return ajustesSemReversaoResult.Error;
        }

        var aLiquidarVencidaResult = await ContarALiquidarVencidaSemLiquidacaoAsync(ct);

        if (aLiquidarVencidaResult.IsFailure)
        {
            return aLiquidarVencidaResult.Error;
        }

        var tributosDivergentesResult = await ContarTributosDivergentesAsync(ct);

        if (tributosDivergentesResult.IsFailure)
        {
            return tributosDivergentesResult.Error;
        }

        return new ResultadoConciliacaoDeResgates(
            resgatesSemAliqResult.Value.Count,
            ajustesSemReversaoResult.Value.Count,
            aLiquidarVencidaResult.Value,
            tributosDivergentesResult.Value);
    }

    private async Task<Result<int>> ContarALiquidarVencidaSemLiquidacaoAsync(CancellationToken ct)
    {
        var candidatasResult = await liquidacaoCandidataReadRepository.ObterAbertasNaoRevertidasAsync(ct);

        if (candidatasResult.IsFailure)
        {
            return candidatasResult.Error;
        }

        var horizonteResult = await calendarioDiasUteisReadRepository.ObterHorizonteAsync(ct);

        if (horizonteResult.IsFailure)
        {
            return horizonteResult.Error;
        }

        var hoje = horizonteResult.Value.Hoje;
        var vencidas = 0;

        foreach (var candidata in candidatasResult.Value)
        {
            var proximoDiaUtilResult = await proximoDiaUtilService.ProximoDiaUtilAsync(candidata.DataEvento, ct);

            if (proximoDiaUtilResult.IsFailure)
            {
                continue;
            }

            if (proximoDiaUtilResult.Value <= hoje)
            {
                vencidas++;
            }
        }

        return vencidas;
    }

    private async Task<Result<int>> ContarTributosDivergentesAsync(CancellationToken ct)
    {
        var candidatosResult = await guardasF5ReadRepository.ObterResgatesTributadosAsync(ct);

        if (candidatosResult.IsFailure)
        {
            return candidatosResult.Error;
        }

        var divergentes = 0;

        foreach (var candidato in candidatosResult.Value)
        {
            var divergenteResult = await ResgateEstaDivergenteAsync(candidato, ct);

            if (divergenteResult.IsFailure)
            {
                return divergenteResult.Error;
            }

            if (divergenteResult.Value)
            {
                divergentes++;
            }
        }

        return divergentes;
    }

    private async Task<Result<bool>> ResgateEstaDivergenteAsync(ResgateTributado candidato, CancellationToken ct)
    {
        var movimentosDoTituloResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            candidato.ClienteId, candidato.InstrumentoId, ct);

        if (movimentosDoTituloResult.IsFailure)
        {
            return movimentosDoTituloResult.Error;
        }

        var movimentosDoTitulo = movimentosDoTituloResult.Value;
        var resgate = movimentosDoTitulo.SingleOrDefault(
            m => m.Tipo == TipoMovimento.Venda && m.RefExterna == candidato.TradeId);

        if (resgate is null)
        {
            return false;
        }

        var rederivacao = RecalculoDeTributos.Rederivar(movimentosDoTitulo, resgate);

        var movimentosALiquidarResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            candidato.ClienteId, InstrumentosCaixa.ALiquidar, ct);

        if (movimentosALiquidarResult.IsFailure)
        {
            return movimentosALiquidarResult.Error;
        }

        var efetivosALiquidar = LivroSemEstornos.MovimentosEfetivos(movimentosALiquidarResult.Value);

        var irEfetivo = efetivosALiquidar.SingleOrDefault(m => m.RefExterna == $"ir:{candidato.TradeId}");
        var iofEfetivo = efetivosALiquidar.SingleOrDefault(m => m.RefExterna == $"iof:{candidato.TradeId}");

        var irAtual = irEfetivo?.ValorFinanceiro ?? 0m;
        var iofAtual = iofEfetivo?.ValorFinanceiro ?? 0m;

        return irAtual != rederivacao.Tributos.Ir || iofAtual != rederivacao.Tributos.Iof;
    }
}

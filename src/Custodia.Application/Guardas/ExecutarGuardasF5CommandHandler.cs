using MediatR;
using Custodia.Application.Backfill;
using Custodia.Application.Calendario;
using Custodia.Application.Liquidacao;
using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Tributos;

namespace Custodia.Application.Guardas;

public sealed class ExecutarGuardasF5CommandHandler(
    IBackfillJanelaF4F5ReadRepository backfillReadRepository,
    ILiquidacaoCandidataReadRepository liquidacaoCandidataReadRepository,
    IGuardasF5ReadRepository guardasF5ReadRepository,
    IMovimentoReadRepository movimentoReadRepository,
    IProximoDiaUtilService proximoDiaUtilService,
    ICalendarioDiasUteisReadRepository calendarioDiasUteisReadRepository)
    : IRequestHandler<ExecutarGuardasF5Command, Result<ResultadoGuardasF5>>
{
    public async Task<Result<ResultadoGuardasF5>> Handle(ExecutarGuardasF5Command request, CancellationToken ct)
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

        return new ResultadoGuardasF5(
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

        var rederivacao = RederivacaoTributosResgate.Rederivar(movimentosDoTitulo, resgate);

        var movimentosALiquidarResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            candidato.ClienteId, InstrumentosCaixa.ALiquidar, ct);

        if (movimentosALiquidarResult.IsFailure)
        {
            return movimentosALiquidarResult.Error;
        }

        var efetivosALiquidar = SequenciaCanonica.MovimentosEfetivos(movimentosALiquidarResult.Value);

        var irEfetivo = efetivosALiquidar.SingleOrDefault(m => m.RefExterna == $"ir:{candidato.TradeId}");
        var iofEfetivo = efetivosALiquidar.SingleOrDefault(m => m.RefExterna == $"iof:{candidato.TradeId}");

        var irAtual = irEfetivo?.ValorFinanceiro ?? 0m;
        var iofAtual = iofEfetivo?.ValorFinanceiro ?? 0m;

        return irAtual != rederivacao.Tributos.Ir || iofAtual != rederivacao.Tributos.Iof;
    }
}

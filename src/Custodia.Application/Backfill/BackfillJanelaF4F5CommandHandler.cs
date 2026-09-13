using MediatR;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Tributos;

namespace Custodia.Application.Backfill;

public sealed class BackfillJanelaF4F5CommandHandler(
    IBackfillJanelaF4F5ReadRepository backfillReadRepository,
    IMovimentoReadRepository movimentoReadRepository,
    IMovimentoWriteRepository movimentoWriteRepository,
    IMovimentoTravamentoRepository movimentoTravamentoRepository,
    IPosicaoCorrenteWriteRepository posicaoCorrenteWriteRepository,
    IAplicadorIncrementalDePosicao aplicadorIncrementalDePosicao,
    IUnitOfWork unitOfWork,
    IBusinessMetrics businessMetrics,
    TimeProvider timeProvider)
    : IRequestHandler<BackfillJanelaF4F5Command, Result<BackfillJanelaF4F5Resultado>>
{
    public async Task<Result<BackfillJanelaF4F5Resultado>> Handle(
        BackfillJanelaF4F5Command request, CancellationToken ct)
    {
        var resgatesResult = await BackfillarResgatesSemAliqAsync(ct);

        if (resgatesResult.IsFailure)
        {
            return resgatesResult.Error;
        }

        var ajustesResult = await BackfillarAjustesDeResgateSemReversaoAsync(ct);

        if (ajustesResult.IsFailure)
        {
            return ajustesResult.Error;
        }

        return new BackfillJanelaF4F5Resultado(
            resgatesResult.Value.Candidatos,
            resgatesResult.Value.Processados,
            ajustesResult.Value.Candidatos,
            ajustesResult.Value.Processados);
    }

    private async Task<Result<(int Candidatos, int Processados)>> BackfillarResgatesSemAliqAsync(CancellationToken ct)
    {
        var candidatosResult = await backfillReadRepository.ObterResgatesSemAliqAsync(ct);

        if (candidatosResult.IsFailure)
        {
            return candidatosResult.Error;
        }

        var candidatos = candidatosResult.Value;
        var processados = 0;

        foreach (var candidato in candidatos)
        {
            var processarResult = await BackfillarUmResgateAsync(candidato, ct);

            if (processarResult.IsFailure)
            {
                return processarResult.Error;
            }

            if (processarResult.Value)
            {
                processados++;
            }
        }

        return (candidatos.Count, processados);
    }

    private async Task<Result<bool>> BackfillarUmResgateAsync(ResgateSemAliq candidato, CancellationToken ct)
    {
        var movimentosDaChaveResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            candidato.ClienteId, candidato.InstrumentoId, ct);

        if (movimentosDaChaveResult.IsFailure)
        {
            return movimentosDaChaveResult.Error;
        }

        var movimentosDaChave = movimentosDaChaveResult.Value;
        var resgate = movimentosDaChave.SingleOrDefault(
            m => m.Tipo == TipoMovimento.Venda && m.RefExterna == candidato.TradeId);

        if (resgate is null)
        {
            return false;
        }

        var refAliq = $"aliq:{candidato.TradeId}";

        var existenteResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
            candidato.ClienteId, refAliq, ct);

        if (existenteResult.IsFailure)
        {
            return existenteResult.Error;
        }

        if (existenteResult.Value.Encontrado)
        {
            return false;
        }

        var rederivacao = RederivacaoTributosResgate.Rederivar(movimentosDaChave, resgate);
        var consumo = rederivacao.Consumo;
        var tributos = rederivacao.Tributos;

        var agora = timeProvider.GetUtcNow();
        var pendencias = new List<Movimento>();

        if (tributos.Ir > 0m)
        {
            var irResult = CriarDerivado(resgate, TipoMovimento.IrRetido, tributos.Ir, $"ir:{candidato.TradeId}", agora);

            if (irResult.IsFailure)
            {
                return irResult.Error;
            }

            pendencias.Add(irResult.Value);
        }

        if (tributos.Iof > 0m)
        {
            var iofResult = CriarDerivado(resgate, TipoMovimento.Iof, tributos.Iof, $"iof:{candidato.TradeId}", agora);

            if (iofResult.IsFailure)
            {
                return iofResult.Error;
            }

            pendencias.Add(iofResult.Value);
        }

        var aliqResult = Movimento.Create(
            candidato.ClienteId,
            InstrumentosCaixa.ALiquidar,
            TipoMovimento.ALiquidar,
            resgate.DataEvento,
            agora,
            qtdDelta: resgate.ValorFinanceiro,
            valorFinanceiro: resgate.ValorFinanceiro,
            refExterna: refAliq);

        if (aliqResult.IsFailure)
        {
            return aliqResult.Error;
        }

        pendencias.Add(aliqResult.Value);

        if (!consumo.CoberturaCompleta)
        {
            businessMetrics.RegistrarResgateTributadoSobrePrecoMedioProvisorio(
                candidato.ClienteId, candidato.InstrumentoId, consumo.QuantidadeDescoberta);
        }

        var gravarResult = await GravarDerivadosAsync(pendencias, ct);

        if (gravarResult.IsFailure)
        {
            return ClassificarFalhaDeGravacao(gravarResult.Error);
        }

        return true;
    }

    private async Task<Result<(int Candidatos, int Processados)>> BackfillarAjustesDeResgateSemReversaoAsync(
        CancellationToken ct)
    {
        var candidatosResult = await backfillReadRepository.ObterAjustesDeResgateSemReversaoAsync(ct);

        if (candidatosResult.IsFailure)
        {
            return candidatosResult.Error;
        }

        var candidatos = candidatosResult.Value;
        var processados = 0;

        foreach (var candidato in candidatos)
        {
            var processarResult = await BackfillarUmAjusteAsync(candidato, ct);

            if (processarResult.IsFailure)
            {
                return processarResult.Error;
            }

            if (processarResult.Value)
            {
                processados++;
            }
        }

        return (candidatos.Count, processados);
    }

    private async Task<Result<bool>> BackfillarUmAjusteAsync(AjusteDeResgateSemReversao candidato, CancellationToken ct)
    {
        var refAliq = $"aliq:{candidato.ResgateTradeId}";

        var travamentoResult = await movimentoTravamentoRepository.TravarPorClienteERefExternaAsync(
            candidato.ClienteId, refAliq, ct);

        if (travamentoResult.IsFailure)
        {
            return travamentoResult.Error;
        }

        if (!travamentoResult.Value.Encontrado)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return false;
        }

        var aliq = travamentoResult.Value.Linha!;
        var refEstAliq = $"est:aliq:{candidato.EstornoTradeId}";

        var estAliqExistenteResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
            candidato.ClienteId, refEstAliq, ct);

        if (estAliqExistenteResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return estAliqExistenteResult.Error;
        }

        if (estAliqExistenteResult.Value.Encontrado)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return false;
        }

        var agora = timeProvider.GetUtcNow();
        var pendencias = new List<Movimento>();

        var ajusteAliqResult = AjusteDeReversao.Criar(aliq, candidato.ClienteId, agora, refEstAliq);

        if (ajusteAliqResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return ajusteAliqResult.Error;
        }

        pendencias.Add(ajusteAliqResult.Value);

        var derivadasParaReverter = new (string RefExternaOriginal, string RefExternaEstorno)[]
        {
            ($"ir:{candidato.ResgateTradeId}", $"est:ir:{candidato.EstornoTradeId}"),
            ($"iof:{candidato.ResgateTradeId}", $"est:iof:{candidato.EstornoTradeId}"),
            ($"liq:{candidato.ResgateTradeId}:aliq", $"est:liq:{candidato.EstornoTradeId}:aliq"),
            ($"liq:{candidato.ResgateTradeId}:brl", $"est:liq:{candidato.EstornoTradeId}:brl"),
        };

        foreach (var (refExternaOriginal, refExternaEstorno) in derivadasParaReverter)
        {
            var derivadaResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
                candidato.ClienteId, refExternaOriginal, ct);

            if (derivadaResult.IsFailure)
            {
                await unitOfWork.DescartarTransacaoAsync(ct);
                return derivadaResult.Error;
            }

            if (!derivadaResult.Value.Encontrado)
            {
                continue;
            }

            var ajusteDerivadaResult = AjusteDeReversao.Criar(
                derivadaResult.Value.Linha!, candidato.ClienteId, agora, refExternaEstorno);

            if (ajusteDerivadaResult.IsFailure)
            {
                await unitOfWork.DescartarTransacaoAsync(ct);
                return ajusteDerivadaResult.Error;
            }

            pendencias.Add(ajusteDerivadaResult.Value);
        }

        var gravarResult = await GravarDerivadosAsync(pendencias, ct);

        if (gravarResult.IsFailure)
        {
            return ClassificarFalhaDeGravacao(gravarResult.Error);
        }

        return true;
    }

    private static Result<Movimento> CriarDerivado(
        Movimento resgate, TipoMovimento tipo, decimal valor, string refExterna, DateTimeOffset registradoEm) =>
        Movimento.Create(
            resgate.ClienteId,
            InstrumentosCaixa.ALiquidar,
            tipo,
            resgate.DataEvento,
            registradoEm,
            qtdDelta: -valor,
            valorFinanceiro: valor,
            refExterna: refExterna);

    private async Task<Result> GravarDerivadosAsync(IReadOnlyList<Movimento> movimentos, CancellationToken ct)
    {
        var lote = new LoteDeAplicacaoDePosicao();

        foreach (var movimento in movimentos)
        {
            var posicaoResult = await aplicadorIncrementalDePosicao.AplicarAsync(lote, movimento, ct);

            if (posicaoResult.IsFailure)
            {
                await unitOfWork.DescartarTransacaoAsync(ct);
                return Result.Failure(posicaoResult.Error);
            }

            var adicionarResult = await movimentoWriteRepository.AdicionarAsync(movimento, ct);

            if (adicionarResult.IsFailure)
            {
                await unitOfWork.DescartarTransacaoAsync(ct);
                return Result.Failure(adicionarResult.Error);
            }

            var atualizarResult = await posicaoCorrenteWriteRepository.AtualizarAsync(
                movimento.ClienteId, movimento.InstrumentoId, posicaoResult.Value, ct);

            if (atualizarResult.IsFailure)
            {
                await unitOfWork.DescartarTransacaoAsync(ct);
                return Result.Failure(atualizarResult.Error);
            }
        }

        return await unitOfWork.SaveChangesAsync(ct);
    }

    private static Result<bool> ClassificarFalhaDeGravacao(Error erro) =>
        erro == MovimentoWriteErrors.MensagemDuplicada || erro == MovimentoWriteErrors.RefEstornoDuplicado
            ? false
            : erro;
}

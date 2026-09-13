using MediatR;
using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Microsoft.Extensions.Configuration;

namespace Custodia.Application.Liquidacao;

public sealed class LiquidarResgatesVencidosCommandHandler(
    ILiquidacaoCandidataReadRepository candidataReadRepository,
    IMovimentoTravamentoRepository movimentoTravamentoRepository,
    IMovimentoReadRepository movimentoReadRepository,
    IMovimentoWriteRepository movimentoWriteRepository,
    IPosicaoCorrenteReadRepository posicaoCorrenteReadRepository,
    IPosicaoCorrenteWriteRepository posicaoCorrenteWriteRepository,
    IUnitOfWork unitOfWork,
    IProximoDiaUtilService proximoDiaUtilService,
    ICalendarioDiasUteisReadRepository calendarioDiasUteisReadRepository,
    IRecalculoEnfileiradorPort recalculoEnfileiradorPort,
    IBusinessMetrics businessMetrics,
    IPontoDeSuspensaoAposTravamento pontoDeSuspensaoAposTravamento,
    IConfiguration configuration,
    TimeProvider timeProvider)
    : IRequestHandler<LiquidarResgatesVencidosCommand, Result<ResultadoLiquidacaoDeResgates>>
{
    public const long TetoPorCicloPadrao = 1000;

    private const string ChaveConfiguracaoTetoPorCiclo = "Decisao:LiquidacaoTetoPorCiclo";

    private enum DesfechoDeCandidata
    {
        Liquidada,
        NaoVencida,
        JaTratada,
    }

    private readonly long _teto = configuration.GetValue<long?>(ChaveConfiguracaoTetoPorCiclo) ?? TetoPorCicloPadrao;

    public async Task<Result<ResultadoLiquidacaoDeResgates>> Handle(
        LiquidarResgatesVencidosCommand request, CancellationToken ct)
    {
        var candidatasResult = await candidataReadRepository.ObterAbertasNaoRevertidasAsync(ct);

        if (candidatasResult.IsFailure)
        {
            return candidatasResult.Error;
        }

        var candidatas = candidatasResult.Value;

        if (candidatas.Count > _teto)
        {
            businessMetrics.RegistrarLiquidacaoLimitePorTeto(candidatas.Count, _teto);
            return LiquidacaoErrors.LimiteDeCandidatasPorCicloExcedido;
        }

        var horizonteResult = await calendarioDiasUteisReadRepository.ObterHorizonteAsync(ct);

        if (horizonteResult.IsFailure)
        {
            return horizonteResult.Error;
        }

        var hoje = horizonteResult.Value.Hoje;

        var fatosLiquidados = 0;
        var fatosNaoVencidos = 0;
        var fatosJaTratados = 0;

        foreach (var candidata in candidatas)
        {
            var processarResult = await ProcessarCandidataAsync(candidata, hoje, ct);

            if (processarResult.IsFailure)
            {
                return processarResult.Error;
            }

            switch (processarResult.Value)
            {
                case DesfechoDeCandidata.Liquidada:
                    fatosLiquidados++;
                    break;
                case DesfechoDeCandidata.NaoVencida:
                    fatosNaoVencidos++;
                    break;
                case DesfechoDeCandidata.JaTratada:
                    fatosJaTratados++;
                    break;
            }
        }

        return new ResultadoLiquidacaoDeResgates(
            DesfechoLiquidacaoDeResgates.Completude, candidatas.Count, fatosLiquidados, fatosNaoVencidos, fatosJaTratados);
    }

    private async Task<Result<DesfechoDeCandidata>> ProcessarCandidataAsync(
        LiquidacaoCandidata candidata, DateOnly hoje, CancellationToken ct)
    {
        var travamentoResult = await movimentoTravamentoRepository.TravarPorClienteERefExternaAsync(
            candidata.ClienteId, $"aliq:{candidata.TradeId}", ct);

        if (travamentoResult.IsFailure)
        {
            return travamentoResult.Error;
        }

        if (!travamentoResult.Value.Encontrado)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return DesfechoDeCandidata.JaTratada;
        }

        var aliq = travamentoResult.Value.Linha!;

        await pontoDeSuspensaoAposTravamento.AposTravarAsync(aliq.ClienteId, aliq.RefExterna, ct);

        var movimentosCaixaALiquidarResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            candidata.ClienteId, InstrumentosCaixa.ALiquidar, ct);

        if (movimentosCaixaALiquidarResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return movimentosCaixaALiquidarResult.Error;
        }

        var movimentosCaixaALiquidar = movimentosCaixaALiquidarResult.Value;

        if (ExisteAjusteRevertendo(movimentosCaixaALiquidar, aliq.Id))
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return DesfechoDeCandidata.JaTratada;
        }

        var refLiqAliq = $"liq:{candidata.TradeId}:aliq";

        if (movimentosCaixaALiquidar.Any(m => m.RefExterna == refLiqAliq))
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return DesfechoDeCandidata.JaTratada;
        }

        var principalResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
            candidata.ClienteId, candidata.TradeId, ct);

        if (principalResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return principalResult.Error;
        }

        if (!principalResult.Value.Encontrado)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return LiquidacaoErrors.MovimentoPrincipalNaoEncontrado;
        }

        var principal = principalResult.Value.Linha!;

        var movimentosDoTituloResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            candidata.ClienteId, principal.InstrumentoId, ct);

        if (movimentosDoTituloResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return movimentosDoTituloResult.Error;
        }

        if (ExisteAjusteRevertendo(movimentosDoTituloResult.Value, principal.Id))
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return DesfechoDeCandidata.JaTratada;
        }

        var proximoDiaUtilResult = await proximoDiaUtilService.ProximoDiaUtilAsync(aliq.DataEvento, ct);

        if (proximoDiaUtilResult.IsFailure)
        {
            businessMetrics.RegistrarLiquidacaoCalendarioExaurido(candidata.ClienteId, candidata.TradeId);
            await unitOfWork.DescartarTransacaoAsync(ct);
            return proximoDiaUtilResult.Error;
        }

        var dataLiquidacao = proximoDiaUtilResult.Value;

        if (dataLiquidacao > hoje)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return DesfechoDeCandidata.NaoVencida;
        }

        var ir = movimentosCaixaALiquidar.SingleOrDefault(m => m.RefExterna == $"ir:{candidata.TradeId}");
        var iof = movimentosCaixaALiquidar.SingleOrDefault(m => m.RefExterna == $"iof:{candidata.TradeId}");
        var saldo = aliq.QtdDelta + (ir?.QtdDelta ?? 0m) + (iof?.QtdDelta ?? 0m);

        var registradoEm = timeProvider.GetUtcNow();

        var pernaAliqResult = Movimento.Create(
            candidata.ClienteId,
            InstrumentosCaixa.ALiquidar,
            TipoMovimento.Liquidacao,
            dataLiquidacao,
            registradoEm,
            qtdDelta: -saldo,
            valorFinanceiro: saldo,
            refExterna: refLiqAliq);

        if (pernaAliqResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return LiquidacaoErrors.PayloadDeLiquidacaoInvalido;
        }

        var pernaBrlResult = Movimento.Create(
            candidata.ClienteId,
            InstrumentosCaixa.Brl,
            TipoMovimento.Liquidacao,
            dataLiquidacao,
            registradoEm,
            qtdDelta: saldo,
            valorFinanceiro: saldo,
            refExterna: $"liq:{candidata.TradeId}:brl");

        if (pernaBrlResult.IsFailure)
        {
            await unitOfWork.DescartarTransacaoAsync(ct);
            return LiquidacaoErrors.PayloadDeLiquidacaoInvalido;
        }

        var gravarResult = await GravarPernasDeLiquidacaoAsync(pernaAliqResult.Value, pernaBrlResult.Value, ct);

        if (gravarResult.IsFailure)
        {
            return gravarResult.Error;
        }

        if (dataLiquidacao < hoje)
        {
            await recalculoEnfileiradorPort.EnfileirarAsync(
                candidata.ClienteId, InstrumentosCaixa.ALiquidar, dataLiquidacao, ct);
            await recalculoEnfileiradorPort.EnfileirarAsync(
                candidata.ClienteId, InstrumentosCaixa.Brl, dataLiquidacao, ct);
        }

        return DesfechoDeCandidata.Liquidada;
    }

    private async Task<Result> GravarPernasDeLiquidacaoAsync(Movimento pernaAliq, Movimento pernaBrl, CancellationToken ct)
    {
        foreach (var perna in new[] { pernaAliq, pernaBrl })
        {
            var posicaoResult = await AplicarNaPosicaoAsync(perna.ClienteId, perna.InstrumentoId, perna, ct);

            if (posicaoResult.IsFailure)
            {
                return Result.Failure(posicaoResult.Error);
            }

            var adicionarResult = await movimentoWriteRepository.AdicionarAsync(perna, ct);

            if (adicionarResult.IsFailure)
            {
                return Result.Failure(adicionarResult.Error);
            }

            var atualizarResult = await posicaoCorrenteWriteRepository.AtualizarAsync(
                perna.ClienteId, perna.InstrumentoId, posicaoResult.Value, ct);

            if (atualizarResult.IsFailure)
            {
                return Result.Failure(atualizarResult.Error);
            }
        }

        return await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Result<PosicaoTresColunas>> AplicarNaPosicaoAsync(
        string clienteId, string instrumentoId, Movimento movimentoRecemCriado, CancellationToken ct)
    {
        var maxDataEventoResult = await movimentoReadRepository.ObterMaxDataEventoAsync(clienteId, instrumentoId, ct);

        if (maxDataEventoResult.IsFailure)
        {
            return maxDataEventoResult.Error;
        }

        var posicaoAtualResult = await posicaoCorrenteReadRepository.ObterAsync(clienteId, instrumentoId, ct);

        if (posicaoAtualResult.IsFailure)
        {
            return posicaoAtualResult.Error;
        }

        var resultadoIncremental = DobraPosicao.AplicarIncremental(
            posicaoAtualResult.Value, movimentoRecemCriado, maxDataEventoResult.Value.ComoNullable());

        if (!resultadoIncremental.ExigeRedobraDaChave)
        {
            return resultadoIncremental.Estado!;
        }

        var movimentosDaChaveResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            clienteId, instrumentoId, ct);

        if (movimentosDaChaveResult.IsFailure)
        {
            return movimentosDaChaveResult.Error;
        }

        var todos = movimentosDaChaveResult.Value.Append(movimentoRecemCriado).ToList();
        return DobraPosicao.Dobrar(todos);
    }

    private static bool ExisteAjusteRevertendo(IReadOnlyList<Movimento> movimentosDaChave, long alvoId) =>
        movimentosDaChave.Any(m => m.Tipo == TipoMovimento.Ajuste && m.RefEstorno == alvoId);
}

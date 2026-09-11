using MediatR;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Eventos;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Eventos;

public sealed class ProcessarTradeRegisteredCommandHandler(
    IMovimentoReadRepository movimentoReadRepository,
    IMovimentoWriteRepository movimentoWriteRepository,
    IPosicaoCorrenteReadRepository posicaoCorrenteReadRepository,
    IPosicaoCorrenteWriteRepository posicaoCorrenteWriteRepository,
    IUnitOfWork unitOfWork,
    IBusinessMetrics businessMetrics)
    : IRequestHandler<ProcessarTradeRegisteredCommand, Result<ResultadoTradeRegistered>>
{
    public async Task<Result<ResultadoTradeRegistered>> Handle(
        ProcessarTradeRegisteredCommand request, CancellationToken ct)
    {
        var evento = request.Evento;

        var existenteResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
            evento.ClienteId, evento.TradeId, ct);

        if (existenteResult.IsFailure)
        {
            return existenteResult.Error;
        }

        if (existenteResult.Value.Encontrado)
        {
            return ResultadoTradeRegistered.Escriturado(replay: true);
        }

        return evento.Operacao switch
        {
            OperacaoTrade.Aplicacao => await ProcessarAplicacaoOuAporteAsync(evento, TipoMovimento.Compra, ct),
            OperacaoTrade.Aporte => await ProcessarAplicacaoOuAporteAsync(evento, TipoMovimento.Aporte, ct),
            OperacaoTrade.Resgate => await ProcessarResgateAsync(evento, ct),
            OperacaoTrade.Estorno => await ProcessarEstornoAsync(evento, ct),
            _ => DomainErrors.General.Unprocessable($"Operação de trade não reconhecida: '{evento.Operacao}'."),
        };
    }

    private async Task<Result<ResultadoTradeRegistered>> ProcessarAplicacaoOuAporteAsync(
        TradeRegisteredEvento evento, TipoMovimento tipo, CancellationToken ct)
    {
        if (evento.ValorOrigemSaldoBruto is null)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.OrigemRecursoAusente);
        }

        if (!DecimalContrato.TryParse(evento.ValorOrigemSaldoBruto, out var valorOrigemSaldo)
            || valorOrigemSaldo < 0m
            || valorOrigemSaldo > evento.ValorFinanceiro)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.OrigemRecursoInvalida);
        }

        var principalResult = Movimento.Create(
            evento.ClienteId,
            evento.InstrumentoId,
            tipo,
            evento.DataEvento,
            evento.RegistradoEm,
            qtdDelta: evento.Quantidade,
            valorFinanceiro: evento.ValorFinanceiro,
            refExterna: evento.TradeId);

        if (principalResult.IsFailure)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.PayloadInvalido);
        }

        var pendencias = new List<Movimento> { principalResult.Value };

        if (valorOrigemSaldo > 0m)
        {
            var pernaResult = Movimento.Create(
                evento.ClienteId,
                InstrumentosCaixa.Brl,
                tipo,
                evento.DataEvento,
                evento.RegistradoEm,
                qtdDelta: -valorOrigemSaldo,
                valorFinanceiro: valorOrigemSaldo,
                refExterna: $"{evento.TradeId}:brl");

            if (pernaResult.IsFailure)
            {
                return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.OrigemRecursoInvalida);
            }

            pendencias.Add(pernaResult.Value);
        }

        return await GravarTudoAsync(pendencias, ct);
    }

    private async Task<Result<ResultadoTradeRegistered>> ProcessarResgateAsync(
        TradeRegisteredEvento evento, CancellationToken ct)
    {
        var movimentoResult = Movimento.Create(
            evento.ClienteId,
            evento.InstrumentoId,
            TipoMovimento.Venda,
            evento.DataEvento,
            evento.RegistradoEm,
            qtdDelta: -evento.Quantidade,
            valorFinanceiro: evento.ValorFinanceiro,
            refExterna: evento.TradeId);

        if (movimentoResult.IsFailure)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.PayloadInvalido);
        }

        return await GravarTudoAsync([movimentoResult.Value], ct);
    }

    private async Task<Result<ResultadoTradeRegistered>> ProcessarEstornoAsync(
        TradeRegisteredEvento evento, CancellationToken ct)
    {
        var estornaTradeId = evento.EstornaTradeId!;

        var mesmoClienteResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
            evento.ClienteId, estornaTradeId, ct);

        if (mesmoClienteResult.IsFailure)
        {
            return mesmoClienteResult.Error;
        }

        Movimento titulo;

        if (mesmoClienteResult.Value.Encontrado)
        {
            titulo = mesmoClienteResult.Value.Linha!;
        }
        else
        {
            var qualquerClienteResult = await movimentoReadRepository.ObterPorRefExternaEmQualquerClienteAsync(
                estornaTradeId, ct);

            if (qualquerClienteResult.IsFailure)
            {
                return qualquerClienteResult.Error;
            }

            if (qualquerClienteResult.Value.Encontrado)
            {
                return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.EstornoClienteDivergente);
            }

            return ResultadoTradeRegistered.EnviarParaRetry();
        }

        if (!ConferenciaBate(evento, titulo))
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.EstornoDivergente);
        }

        var ajusteTituloResult = Movimento.Create(
            evento.ClienteId,
            titulo.InstrumentoId,
            TipoMovimento.Ajuste,
            titulo.DataEvento,
            evento.RegistradoEm,
            qtdDelta: -titulo.QtdDelta,
            valorFinanceiro: -titulo.ValorFinanceiro,
            refExterna: evento.TradeId,
            refEstorno: titulo.Id);

        if (ajusteTituloResult.IsFailure)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.PayloadInvalido);
        }

        var pendencias = new List<Movimento> { ajusteTituloResult.Value };

        var pernaOriginalResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
            evento.ClienteId, $"{estornaTradeId}:brl", ct);

        if (pernaOriginalResult.IsFailure)
        {
            return pernaOriginalResult.Error;
        }

        if (pernaOriginalResult.Value.Encontrado)
        {
            var pernaOriginal = pernaOriginalResult.Value.Linha!;

            var ajustePernaResult = Movimento.Create(
                evento.ClienteId,
                pernaOriginal.InstrumentoId,
                TipoMovimento.Ajuste,
                pernaOriginal.DataEvento,
                evento.RegistradoEm,
                qtdDelta: -pernaOriginal.QtdDelta,
                valorFinanceiro: -pernaOriginal.ValorFinanceiro,
                refExterna: $"{evento.TradeId}:brl",
                refEstorno: pernaOriginal.Id);

            if (ajustePernaResult.IsFailure)
            {
                return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.PayloadInvalido);
            }

            pendencias.Add(ajustePernaResult.Value);
        }

        return await GravarTudoAsync(pendencias, ct);
    }

    private static bool ConferenciaBate(TradeRegisteredEvento evento, Movimento titulo) =>
        evento.InstrumentoId == titulo.InstrumentoId
        && evento.Quantidade == Math.Abs(titulo.QtdDelta)
        && evento.ValorFinanceiro == titulo.ValorFinanceiro;

    private async Task<Result<ResultadoTradeRegistered>> GravarTudoAsync(
        IReadOnlyList<Movimento> movimentos, CancellationToken ct)
    {
        foreach (var movimento in movimentos)
        {
            var posicaoResult = await AplicarNaPosicaoAsync(
                movimento.ClienteId, movimento.InstrumentoId, movimento, ct);

            if (posicaoResult.IsFailure)
            {
                return posicaoResult.Error;
            }

            var adicionarResult = await movimentoWriteRepository.AdicionarAsync(movimento, ct);
            if (adicionarResult.IsFailure)
            {
                return adicionarResult.Error;
            }

            var atualizarResult = await posicaoCorrenteWriteRepository.AtualizarAsync(
                movimento.ClienteId, movimento.InstrumentoId, posicaoResult.Value, ct);

            if (atualizarResult.IsFailure)
            {
                return atualizarResult.Error;
            }

            var sinalizarResult = await SinalizarSeNecessarioAsync(
                movimento.ClienteId, movimento.InstrumentoId, posicaoResult.Value, ct);

            if (sinalizarResult.IsFailure)
            {
                return sinalizarResult.Error;
            }
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);

        return saveResult.IsFailure
            ? ClassificarFalhaDeGravacao(saveResult.Error)
            : ResultadoTradeRegistered.Escriturado();
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

    private async Task<Result> SinalizarSeNecessarioAsync(
        string clienteId, string instrumentoId, PosicaoTresColunas estado, CancellationToken ct)
    {
        if (!InstrumentosCaixa.Todos.Contains(instrumentoId))
        {
            if (estado.Quantidade < 0m)
            {
                businessMetrics.RegistrarPosicaoNegativaSinalizada(clienteId, instrumentoId, estado.Quantidade);
            }

            return Result.Success();
        }

        if (instrumentoId != InstrumentosCaixa.Brl)
        {
            return Result.Success();
        }

        var aLiquidarResult = await posicaoCorrenteReadRepository.ObterAsync(
            clienteId, InstrumentosCaixa.ALiquidar, ct);

        if (aLiquidarResult.IsFailure)
        {
            return Result.Failure(aLiquidarResult.Error);
        }

        var somaCaixa = estado.Quantidade + aLiquidarResult.Value.Quantidade;

        if (somaCaixa < 0m)
        {
            businessMetrics.RegistrarPosicaoNegativaSinalizada(clienteId, InstrumentosCaixa.Brl, somaCaixa);
        }

        return Result.Success();
    }

    private static Result<ResultadoTradeRegistered> ClassificarFalhaDeGravacao(Error erro)
    {
        if (erro == MovimentoWriteErrors.MensagemDuplicada)
        {
            return ResultadoTradeRegistered.Escriturado(replay: true);
        }

        if (erro == MovimentoWriteErrors.RefEstornoDuplicado)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.EstornoDuplicado);
        }

        if (erro == MovimentoWriteErrors.IdentificadorComEspacoNaBorda)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.IdentificadorComEspacoNaBorda);
        }

        if (erro == MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.PayloadInvalido);
        }

        if (erro == MovimentoWriteErrors.DataEventoFutura)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoEstacionamento.PayloadInvalido);
        }

        if (erro == MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel)
        {
            return erro;
        }

        return erro;
    }
}

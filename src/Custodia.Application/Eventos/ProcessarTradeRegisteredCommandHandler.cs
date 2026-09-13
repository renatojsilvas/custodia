using MediatR;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Eventos;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Tributos;

namespace Custodia.Application.Eventos;

public sealed class ProcessarTradeRegisteredCommandHandler(
    IMovimentoReadRepository movimentoReadRepository,
    IMovimentoWriteRepository movimentoWriteRepository,
    IMovimentoTravamentoRepository movimentoTravamentoRepository,
    IPosicaoCorrenteReadRepository posicaoCorrenteReadRepository,
    IPosicaoCorrenteWriteRepository posicaoCorrenteWriteRepository,
    IAplicadorIncrementalDePosicao aplicadorIncrementalDePosicao,
    IUnitOfWork unitOfWork,
    IBusinessMetrics businessMetrics,
    IPontoDeSuspensaoAposTravamento pontoDeSuspensaoAposTravamento)
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
            return ResultadoTradeRegistered.Estacionar(MotivoParking.OrigemRecursoAusente);
        }

        if (!DecimalContrato.TryParse(evento.ValorOrigemSaldoBruto, out var valorOrigemSaldo)
            || valorOrigemSaldo < 0m
            || valorOrigemSaldo > evento.ValorFinanceiro)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.OrigemRecursoInvalida);
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
            return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
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
                return ResultadoTradeRegistered.Estacionar(MotivoParking.OrigemRecursoInvalida);
            }

            pendencias.Add(pernaResult.Value);
        }

        return await GravarTudoAsync(pendencias, ct);
    }

    private async Task<Result<ResultadoTradeRegistered>> ProcessarResgateAsync(
        TradeRegisteredEvento evento, CancellationToken ct)
    {
        var vendaResult = Movimento.Create(
            evento.ClienteId,
            evento.InstrumentoId,
            TipoMovimento.Venda,
            evento.DataEvento,
            evento.RegistradoEm,
            qtdDelta: -evento.Quantidade,
            valorFinanceiro: evento.ValorFinanceiro,
            refExterna: evento.TradeId);

        if (vendaResult.IsFailure)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
        }

        var movimentosDaChaveResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            evento.ClienteId, evento.InstrumentoId, ct);

        if (movimentosDaChaveResult.IsFailure)
        {
            return movimentosDaChaveResult.Error;
        }

        var rederivacao = RecalculoDeTributos.Rederivar(movimentosDaChaveResult.Value, vendaResult.Value);
        var consumo = rederivacao.Consumo;
        var tributos = rederivacao.Tributos;

        if (!consumo.CoberturaCompleta)
        {
            businessMetrics.RegistrarResgateTributadoSobrePrecoMedioProvisorio(
                evento.ClienteId, evento.InstrumentoId, consumo.QuantidadeDescoberta);
        }

        var pendencias = new List<Movimento> { vendaResult.Value };

        if (tributos.Ir > 0m)
        {
            var irResult = CriarMovimentoDeTributo(evento, TipoMovimento.IrRetido, tributos.Ir, $"ir:{evento.TradeId}");
            if (irResult.IsFailure)
            {
                return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
            }

            pendencias.Add(irResult.Value);
        }

        if (tributos.Iof > 0m)
        {
            var iofResult = CriarMovimentoDeTributo(evento, TipoMovimento.Iof, tributos.Iof, $"iof:{evento.TradeId}");
            if (iofResult.IsFailure)
            {
                return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
            }

            pendencias.Add(iofResult.Value);
        }

        var aliqResult = Movimento.Create(
            evento.ClienteId,
            InstrumentosCaixa.ALiquidar,
            TipoMovimento.ALiquidar,
            evento.DataEvento,
            evento.RegistradoEm,
            qtdDelta: evento.ValorFinanceiro,
            valorFinanceiro: evento.ValorFinanceiro,
            refExterna: $"aliq:{evento.TradeId}");

        if (aliqResult.IsFailure)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
        }

        pendencias.Add(aliqResult.Value);

        return await GravarTudoAsync(pendencias, ct);
    }

    private static Result<Movimento> CriarMovimentoDeTributo(
        TradeRegisteredEvento evento, TipoMovimento tipo, decimal valor, string refExterna) =>
        Movimento.Create(
            evento.ClienteId,
            InstrumentosCaixa.ALiquidar,
            tipo,
            evento.DataEvento,
            evento.RegistradoEm,
            qtdDelta: -valor,
            valorFinanceiro: valor,
            refExterna: refExterna);

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
                return ResultadoTradeRegistered.Estacionar(MotivoParking.EstornoClienteDivergente);
            }

            return ResultadoTradeRegistered.EnviarParaRetry();
        }

        if (!ConferenciaBate(evento, titulo))
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.EstornoDivergente);
        }

        var ajusteTituloResult = AjusteDeReversao.Criar(titulo, evento.ClienteId, evento.RegistradoEm, evento.TradeId);

        if (ajusteTituloResult.IsFailure)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
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

            var ajustePernaResult = AjusteDeReversao.Criar(
                pernaOriginal, evento.ClienteId, evento.RegistradoEm, $"{evento.TradeId}:brl");

            if (ajustePernaResult.IsFailure)
            {
                return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
            }

            pendencias.Add(ajustePernaResult.Value);
        }

        if (titulo.Tipo == TipoMovimento.Venda)
        {
            var derivadosResult = await ReverterDerivadosDoResgateAsync(evento, estornaTradeId, pendencias, ct);

            if (derivadosResult.IsFailure)
            {
                return derivadosResult.Error;
            }

            if (derivadosResult.Value.DeveInterromper)
            {
                return derivadosResult.Value.ResultadoDeInterrupcao!;
            }
        }

        return await GravarTudoAsync(pendencias, ct);
    }

    private readonly record struct ResultadoReversaoDeDerivados(bool DeveInterromper, ResultadoTradeRegistered? ResultadoDeInterrupcao)
    {
        public static readonly ResultadoReversaoDeDerivados Continuar = new(false, null);

        public static ResultadoReversaoDeDerivados Interromper(ResultadoTradeRegistered resultado) => new(true, resultado);
    }

    private async Task<Result<ResultadoReversaoDeDerivados>> ReverterDerivadosDoResgateAsync(
        TradeRegisteredEvento evento, string estornaTradeId, List<Movimento> pendencias, CancellationToken ct)
    {
        var travamentoResult = await movimentoTravamentoRepository.TravarPorClienteERefExternaAsync(
            evento.ClienteId, $"aliq:{estornaTradeId}", ct);

        if (travamentoResult.IsFailure)
        {
            return travamentoResult.Error;
        }

        if (!travamentoResult.Value.Encontrado)
        {
            return ResultadoReversaoDeDerivados.Continuar;
        }

        var aliq = travamentoResult.Value.Linha!;

        await pontoDeSuspensaoAposTravamento.AposTravarAsync(aliq.ClienteId, aliq.RefExterna, ct);

        var ajusteAliqResult = AjusteDeReversao.Criar(
            aliq, evento.ClienteId, evento.RegistradoEm, $"est:aliq:{evento.TradeId}");

        if (ajusteAliqResult.IsFailure)
        {
            return ResultadoReversaoDeDerivados.Interromper(
                ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido));
        }

        pendencias.Add(ajusteAliqResult.Value);

        var derivadasParaReverter = new (string RefExternaOriginal, string RefExternaEstorno)[]
        {
            ($"ir:{estornaTradeId}", $"est:ir:{evento.TradeId}"),
            ($"iof:{estornaTradeId}", $"est:iof:{evento.TradeId}"),
            ($"liq:{estornaTradeId}:aliq", $"est:liq:{evento.TradeId}:aliq"),
            ($"liq:{estornaTradeId}:brl", $"est:liq:{evento.TradeId}:brl"),
        };

        foreach (var (refExternaOriginal, refExternaEstorno) in derivadasParaReverter)
        {
            var derivadaResult = await movimentoReadRepository.ObterPorClienteERefExternaAsync(
                evento.ClienteId, refExternaOriginal, ct);

            if (derivadaResult.IsFailure)
            {
                return derivadaResult.Error;
            }

            if (!derivadaResult.Value.Encontrado)
            {
                continue;
            }

            var ajusteDerivadaResult = AjusteDeReversao.Criar(
                derivadaResult.Value.Linha!, evento.ClienteId, evento.RegistradoEm, refExternaEstorno);

            if (ajusteDerivadaResult.IsFailure)
            {
                return ResultadoReversaoDeDerivados.Interromper(
                    ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido));
            }

            pendencias.Add(ajusteDerivadaResult.Value);
        }

        return ResultadoReversaoDeDerivados.Continuar;
    }

    private static bool ConferenciaBate(TradeRegisteredEvento evento, Movimento titulo) =>
        evento.InstrumentoId == titulo.InstrumentoId
        && evento.Quantidade == Math.Abs(titulo.QtdDelta)
        && evento.ValorFinanceiro == titulo.ValorFinanceiro;

    private async Task<Result<ResultadoTradeRegistered>> GravarTudoAsync(
        IReadOnlyList<Movimento> movimentos, CancellationToken ct)
    {
        var lote = new LoteDeAplicacaoDePosicao();

        foreach (var movimento in movimentos)
        {
            var posicaoResult = await aplicadorIncrementalDePosicao.AplicarAsync(lote, movimento, ct);

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
            return ResultadoTradeRegistered.Estacionar(MotivoParking.EstornoDuplicado);
        }

        if (erro == MovimentoWriteErrors.IdentificadorComEspacoNaBorda)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.IdentificadorComEspacoNaBorda);
        }

        if (erro == MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
        }

        if (erro == MovimentoWriteErrors.DataEventoFutura)
        {
            return ResultadoTradeRegistered.Estacionar(MotivoParking.PayloadInvalido);
        }

        if (erro == MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel)
        {
            return erro;
        }

        return erro;
    }
}

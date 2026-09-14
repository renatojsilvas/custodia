using Custodia.Application.Common.Interfaces;
using Custodia.Application.Eventos;
using Custodia.Domain.Common;
using MediatR;

namespace Custodia.Application.Precos;

public sealed class ProcessarPriceObservedCommandHandler(
    IPrecoWriteRepository precoWriteRepository,
    IUnitOfWork unitOfWork,
    IBusinessMetrics businessMetrics)
    : IRequestHandler<ProcessarPriceObservedCommand, Result<ResultadoPriceObserved>>
{
    public async Task<Result<ResultadoPriceObserved>> Handle(
        ProcessarPriceObservedCommand request, CancellationToken ct)
    {
        var evento = request.Evento;
        var observacao = new ObservacaoDePreco(
            evento.InstrumentoId, evento.DataRef, evento.Campo, evento.Fonte, evento.Valor, evento.Revisao, evento.ObservadoEm);

        var historicoResult = await precoWriteRepository.RegistrarHistoricoAsync(observacao, ct);

        if (historicoResult.IsFailure)
        {
            return historicoResult.Error;
        }

        Result<ResultadoPriceObserved> resultadoFinal;

        switch (historicoResult.Value.Tipo)
        {
            case ResultadoHistoricoTipo.Divergente:
                businessMetrics.RegistrarValorDivergenteNoHistoricoDePrecos(
                    evento.InstrumentoId,
                    evento.Campo,
                    evento.DataRef,
                    evento.Fonte,
                    evento.Revisao,
                    historicoResult.Value.ValorAnterior!.Value,
                    evento.Valor);
                resultadoFinal = ResultadoPriceObserved.ValorDivergente(historicoResult.Value.ValorAnterior.Value, evento.Valor);
                break;

            case ResultadoHistoricoTipo.ReplayInocuo:
                resultadoFinal = ResultadoPriceObserved.ReplaySemAlteracao();
                break;

            case ResultadoHistoricoTipo.Inserido:
                var aplicarResult = await AplicarPrecoAtualAsync(evento, observacao, ct);
                if (aplicarResult.IsFailure)
                {
                    return aplicarResult.Error;
                }

                resultadoFinal = aplicarResult.Value;
                break;

            default:
                throw new InvalidOperationException(
                    $"Resultado de histórico de preço não reconhecido: '{historicoResult.Value.Tipo}'.");
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);

        return saveResult.IsFailure ? saveResult.Error : resultadoFinal;
    }

    private async Task<Result<ResultadoPriceObserved>> AplicarPrecoAtualAsync(
        PriceObservedEvento evento, ObservacaoDePreco observacao, CancellationToken ct)
    {
        if (evento.Revisao > 0)
        {
            var valorAnteriorResult = await RegistrarRevisaoRecebidaAsync(evento, observacao, ct);
            if (valorAnteriorResult.IsFailure)
            {
                return valorAnteriorResult.Error;
            }
        }

        var atualizacaoResult = await precoWriteRepository.AtualizarPrecoAtualAsync(observacao, ct);

        if (atualizacaoResult.IsFailure)
        {
            return atualizacaoResult.Error;
        }

        return atualizacaoResult.Value.Tipo == ResultadoAtualizacaoPrecoAtualTipo.Atualizado
            ? ResultadoPriceObserved.AplicadoEmPrecoAtual()
            : ResultadoPriceObserved.SoHistorico(atualizacaoResult.Value.Motivo!.Value);
    }

    private async Task<Result> RegistrarRevisaoRecebidaAsync(
        PriceObservedEvento evento, ObservacaoDePreco observacao, CancellationToken ct)
    {
        var valorAnteriorResult = await precoWriteRepository.ObterValorRevisaoAnteriorAsync(observacao, ct);

        if (valorAnteriorResult.IsFailure)
        {
            return Result.Failure(valorAnteriorResult.Error);
        }

        businessMetrics.RegistrarRevisaoDePrecoRecebida(
            evento.InstrumentoId,
            evento.Campo,
            evento.DataRef,
            evento.Revisao,
            evento.Fonte,
            evento.Valor,
            valorAnteriorResult.Value.ComoNullable());

        return Result.Success();
    }
}

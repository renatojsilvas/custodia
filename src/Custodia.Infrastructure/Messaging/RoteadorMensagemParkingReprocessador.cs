using System.Text;
using System.Text.Json;
using Custodia.Application.Eventos;
using Custodia.Application.Precos;
using Custodia.Domain.Eventos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Custodia.Infrastructure.Messaging;

public sealed class RoteadorMensagemParkingReprocessador(
    IServiceScopeFactory scopeFactory, RoteadorDeEventos roteador) : IMensagemParkingReprocessador
{
    public async Task<ResultadoReprocessamento> ReprocessarAsync(
        string routingKey, ReadOnlyMemory<byte> corpo, MotivoParking motivoOriginal, CancellationToken ct)
    {
        var corpoTexto = Encoding.UTF8.GetString(corpo.Span);

        DesfechoRoteamento desfecho;
        try
        {
            desfecho = roteador.Rotear(routingKey, corpoTexto);
        }
        catch (JsonException)
        {
            return ResultadoReprocessamento.Falha(MotivoParking.PayloadInvalido);
        }

        switch (desfecho.Tipo)
        {
            case DesfechoRoteamentoTipo.Ignorar:
                return ResultadoReprocessamento.Ignorar();

            case DesfechoRoteamentoTipo.Estacionar:
                return ResultadoReprocessamento.Falha(desfecho.Motivo!);

            case DesfechoRoteamentoTipo.Escriturar:
                return await EscriturarAsync(desfecho.Evento!, motivoOriginal, ct);

            case DesfechoRoteamentoTipo.Observar:
                return await ObservarAsync(desfecho.EventoPreco!, motivoOriginal, ct);

            default:
                throw new InvalidOperationException($"Desfecho de roteamento não reconhecido: '{desfecho.Tipo}'.");
        }
    }

    private async Task<ResultadoReprocessamento> ObservarAsync(
        PriceObservedEvento evento, MotivoParking motivoOriginal, CancellationToken ct)
    {
        using var escopo = scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
        var resultado = await mediator.Send(new ProcessarPriceObservedCommand(evento), ct);

        return resultado.IsSuccess
            ? ResultadoReprocessamento.Sucesso()
            : ResultadoReprocessamento.Falha(motivoOriginal);
    }

    private async Task<ResultadoReprocessamento> EscriturarAsync(
        TradeRegisteredEvento evento, MotivoParking motivoOriginal, CancellationToken ct)
    {
        using var escopo = scopeFactory.CreateScope();
        var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
        var resultado = await mediator.Send(new ProcessarTradeRegisteredCommand(evento), ct);

        if (resultado.IsFailure)
        {
            return ResultadoReprocessamento.Falha(motivoOriginal);
        }

        return resultado.Value.Tipo switch
        {
            ResultadoTradeRegisteredTipo.Escriturado => ResultadoReprocessamento.Sucesso(),
            ResultadoTradeRegisteredTipo.Estacionar => ResultadoReprocessamento.Falha(resultado.Value.Motivo!),
            ResultadoTradeRegisteredTipo.EnviarParaRetry => ResultadoReprocessamento.Falha(motivoOriginal),
            _ => ResultadoReprocessamento.Falha(motivoOriginal),
        };
    }
}

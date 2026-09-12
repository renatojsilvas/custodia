using System.Text;
using System.Text.Json;
using Custodia.Application.Eventos;
using Custodia.Domain.Eventos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Custodia.Infrastructure.Messaging;

public sealed class RoteadorMensagemParkeadaReprocessador(
    IServiceScopeFactory scopeFactory, RoteadorDeEventos roteador) : IMensagemParkeadaReprocessador
{
    public async Task<ResultadoReprocessamento> ReprocessarAsync(
        string routingKey, ReadOnlyMemory<byte> corpo, MotivoEstacionamento motivoOriginal, CancellationToken ct)
    {
        var corpoTexto = Encoding.UTF8.GetString(corpo.Span);

        DesfechoRoteamento desfecho;
        try
        {
            desfecho = roteador.Rotear(routingKey, corpoTexto);
        }
        catch (JsonException)
        {
            return ResultadoReprocessamento.Falha(MotivoEstacionamento.PayloadInvalido);
        }

        switch (desfecho.Tipo)
        {
            case DesfechoRoteamentoTipo.Ignorar:
                return ResultadoReprocessamento.Ignorar();

            case DesfechoRoteamentoTipo.Estacionar:
                return ResultadoReprocessamento.Falha(desfecho.Motivo!);

            case DesfechoRoteamentoTipo.Escriturar:
                return await EscriturarAsync(desfecho.Evento!, motivoOriginal, ct);

            default:
                return ResultadoReprocessamento.Falha(motivoOriginal);
        }
    }

    private async Task<ResultadoReprocessamento> EscriturarAsync(
        TradeRegisteredEvento evento, MotivoEstacionamento motivoOriginal, CancellationToken ct)
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

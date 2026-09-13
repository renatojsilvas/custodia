using Custodia.Application.Liquidacao;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Liquidacao;

public sealed class LiquidacaoDeResgatesJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuracao,
    ILogger<LiquidacaoDeResgatesJob> logger) : BackgroundService
{
    public const int CicloIntervaloSegundosPadrao = 60;

    private const string ChaveConfiguracaoCicloIntervaloSegundos = "Decisao:LiquidacaoCicloIntervaloSegundos";

    private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(
        configuracao.GetValue<int?>(ChaveConfiguracaoCicloIntervaloSegundos) ?? CicloIntervaloSegundosPadrao);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            await ExecutarUmCicloAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task ExecutarUmCicloAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = scopeFactory.CreateScope();
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();

            var resultado = await mediator.Send(new LiquidarResgatesVencidosCommand(), ct);

            if (resultado.IsFailure)
            {
                logger.LogError(
                    "Ciclo do job de liquidação de resgates falhou: {CodigoDeErro} - {Mensagem}. " +
                    "Nova tentativa no próximo ciclo.",
                    resultado.Error.Code,
                    resultado.Error.Description);
                return;
            }

            logger.LogInformation(
                "Ciclo do job de liquidação de resgates concluído por COMPLETUDE: {CandidatasExaminadas} " +
                "candidatas examinadas, {FatosLiquidados} liquidados, {FatosNaoVencidos} ainda não vencidos, " +
                "{FatosJaTratados} já tratados (revertidos ou liquidados por outra execução).",
                resultado.Value.CandidatasExaminadas,
                resultado.Value.FatosLiquidados,
                resultado.Value.FatosNaoVencidos,
                resultado.Value.FatosJaTratados);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha inesperada no ciclo do job de liquidação de resgates; nova tentativa no próximo ciclo.");
        }
    }
}

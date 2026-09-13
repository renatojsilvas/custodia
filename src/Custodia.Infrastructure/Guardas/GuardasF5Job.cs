using Custodia.Application.Common.Interfaces;
using Custodia.Application.Guardas;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Guardas;

public sealed class GuardasF5Job(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuracao,
    ILogger<GuardasF5Job> logger) : BackgroundService
{
    public const int CicloIntervaloSegundosPadrao = 300;

    private const string ChaveConfiguracaoCicloIntervaloSegundos = "Decisao:GuardasF5CicloIntervaloSegundos";

    private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(
        configuracao.GetValue<int?>(ChaveConfiguracaoCicloIntervaloSegundos) ?? CicloIntervaloSegundosPadrao);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            await ExecutarUmaVarreduraAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task ExecutarUmaVarreduraAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = scopeFactory.CreateScope();
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
            var businessMetrics = escopo.ServiceProvider.GetRequiredService<IBusinessMetrics>();

            var resultado = await mediator.Send(new ExecutarGuardasF5Command(), ct);

            if (resultado.IsFailure)
            {
                logger.LogError(
                    "Varredura das guardas permanentes do F5 falhou: {CodigoDeErro} - {Mensagem}. " +
                    "Nova tentativa no próximo ciclo.",
                    resultado.Error.Code,
                    resultado.Error.Description);
                return;
            }

            businessMetrics.RegistrarGuardaResgatesSemAliq(resultado.Value.ResgatesSemAliq);
            businessMetrics.RegistrarGuardaAjustesDeResgateSemReversao(resultado.Value.AjustesDeResgateSemReversao);
            businessMetrics.RegistrarGuardaALiquidarVencidaSemLiquidacao(resultado.Value.ALiquidarVencidaSemLiquidacao);
            businessMetrics.RegistrarGuardaTributoDivergenteDoRederivado(resultado.Value.TributosDivergentesDoRederivado);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha inesperada na varredura das guardas permanentes do F5; nova tentativa no próximo ciclo.");
        }
    }
}

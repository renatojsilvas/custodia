using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Calendario;

public sealed class CalendarioDiasUteisHorizonteGuard(
    IServiceScopeFactory scopeFactory,
    IBusinessMetrics metrics,
    IConfiguration configuracao,
    ILogger<CalendarioDiasUteisHorizonteGuard> logger) : BackgroundService
{
    public const int DiasMinimosPadrao = 90;

    private const string ChaveConfiguracaoDiasMinimos = "CalendarioDiasUteis:AlertaHorizonteDiasMinimos";

    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private readonly int _diasMinimosConfigurados = configuracao.GetValue<int?>(ChaveConfiguracaoDiasMinimos) ?? DiasMinimosPadrao;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);

        do
        {
            await VerificarUmaVezAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task VerificarUmaVezAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = scopeFactory.CreateScope();
            var repositorio = escopo.ServiceProvider.GetRequiredService<ICalendarioDiasUteisReadRepository>();

            var resultado = await repositorio.ObterHorizonteAsync(ct);
            if (resultado.IsFailure)
            {
                logger.LogWarning(
                    "Falha ao consultar o horizonte do calendário de dias úteis; nova tentativa no próximo ciclo. {CodigoDeErro}",
                    resultado.Error.Code);
                return;
            }

            metrics.RegistrarHorizonteCalendarioDiasUteis(resultado.Value.DiasRestantes(), _diasMinimosConfigurados);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao verificar o horizonte do calendário de dias úteis; nova tentativa no próximo ciclo.");
        }
    }
}

using Custodia.Application.Common.Interfaces;
using Custodia.Application.Precos.Bootstrap;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Conciliacao;

public sealed class ConciliacaoDePrecosJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuracao,
    ILogger<ConciliacaoDePrecosJob> logger) : BackgroundService
{
    public const int CicloIntervaloSegundosPadrao = 900;

    private const string ChaveConfiguracaoCicloIntervaloSegundos = "Decisao:ConciliacaoDePrecosCicloIntervaloSegundos";
    private const string DesfechoCompletude = "completude";
    private const string DesfechoFalha = "falha";

    private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(
        configuracao.GetValue<int?>(ChaveConfiguracaoCicloIntervaloSegundos) ?? CicloIntervaloSegundosPadrao);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_intervalo);

        do
        {
            await ExecutarUmaVoltaAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task ExecutarUmaVoltaAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = scopeFactory.CreateScope();
            var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
            var businessMetrics = escopo.ServiceProvider.GetRequiredService<IBusinessMetrics>();

            var resultado = await mediator.Send(
                new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.SemPrecoAtualSoHoje, Desde: null, Ate: null), ct);

            if (resultado.IsFailure)
            {
                businessMetrics.RegistrarConciliacaoDePrecosVolta(DesfechoFalha);
                logger.LogError(
                    "Conciliação de preços falhou: {CodigoDeErro} - {Mensagem}. Nova tentativa no próximo ciclo.",
                    resultado.Error.Code,
                    resultado.Error.Description);
                return;
            }

            businessMetrics.RegistrarConciliacaoDePrecosSemPrecoAtual(resultado.Value.Instrumentos);
            businessMetrics.RegistrarConciliacaoDePrecosVolta(DesfechoCompletude);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha inesperada na conciliação de preços; nova tentativa no próximo ciclo.");
        }
    }
}

using Custodia.Application.Common.Interfaces;
using Custodia.Application.Precos.Bootstrap;
using Custodia.Application.Precos.Hub;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Conciliacao;
using Custodia.Infrastructure.Tests.Calendario;
using Custodia.Infrastructure.Tests.Observability;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Tests.Conciliacao;

public sealed class ConciliacaoDePrecosJobTests
{
    private static IConfiguration ConfiguracaoSemSecao() => new ConfigurationBuilder().Build();

    private static (IServiceScopeFactory ScopeFactory, FakeBusinessMetrics Metrics, FakeMediatorParaConciliacaoDePrecos Mediator)
        FabricaDeEscoposCom(Result<ResultadoColetaDePrecos> resultado)
    {
        var mediator = new FakeMediatorParaConciliacaoDePrecos(resultado);
        var metrics = new FakeBusinessMetrics();
        var services = new ServiceCollection();
        services.AddScoped<IMediator>(_ => mediator);
        services.AddScoped<IBusinessMetrics>(_ => metrics);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return (scopeFactory, metrics, mediator);
    }

    [Fact]
    public async Task ExecutarUmaVoltaAsync_ComSucesso_RegistraOGaugeEODesfechoDeCompletude()
    {
        var resultado = Result<ResultadoColetaDePrecos>.Success(
            new ResultadoColetaDePrecos(1, 1, 3, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0));
        var (scopeFactory, metrics, mediator) = FabricaDeEscoposCom(resultado);
        var logger = new FakeLogger<ConciliacaoDePrecosJob>();
        var job = new ConciliacaoDePrecosJob(scopeFactory, ConfiguracaoSemSecao(), logger);

        await job.ExecutarUmaVoltaAsync(CancellationToken.None);

        var comando = Assert.Single(mediator.ComandosRecebidos);
        Assert.Equal(EscopoDeColetaDePrecos.SemPrecoAtualSoHoje, comando.Escopo);

        Assert.Equal([3], metrics.ConciliacaoDePrecosSemPrecoAtual);
        Assert.Equal(["completude"], metrics.ConciliacaoDePrecosVoltas);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task ExecutarUmaVoltaAsync_ComEscopoVazio_RegistraGaugeZero()
    {
        var resultado = Result<ResultadoColetaDePrecos>.Success(ResultadoColetaDePrecos.EscopoVazio);
        var (scopeFactory, metrics, _) = FabricaDeEscoposCom(resultado);
        var job = new ConciliacaoDePrecosJob(scopeFactory, ConfiguracaoSemSecao(), new FakeLogger<ConciliacaoDePrecosJob>());

        await job.ExecutarUmaVoltaAsync(CancellationToken.None);

        Assert.Equal([0], metrics.ConciliacaoDePrecosSemPrecoAtual);
        Assert.Equal(["completude"], metrics.ConciliacaoDePrecosVoltas);
    }

    [Fact]
    public async Task ExecutarUmaVoltaAsync_ComFalha_RegistraDesfechoDeFalhaELogaErroSemLancar()
    {
        var resultado = Result<ResultadoColetaDePrecos>.Failure(HubPrecosErrors.HubIndisponivel);
        var (scopeFactory, metrics, _) = FabricaDeEscoposCom(resultado);
        var logger = new FakeLogger<ConciliacaoDePrecosJob>();
        var job = new ConciliacaoDePrecosJob(scopeFactory, ConfiguracaoSemSecao(), logger);

        await job.ExecutarUmaVoltaAsync(CancellationToken.None);

        Assert.Equal(["falha"], metrics.ConciliacaoDePrecosVoltas);
        Assert.Empty(metrics.ConciliacaoDePrecosSemPrecoAtual);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }
}

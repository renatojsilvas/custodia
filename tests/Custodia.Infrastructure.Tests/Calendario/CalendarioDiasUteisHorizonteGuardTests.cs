using Custodia.Application.Calendario;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Calendario;
using Custodia.Infrastructure.Tests.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Tests.Calendario;

public sealed class CalendarioDiasUteisHorizonteGuardTests
{
    private static IConfiguration ConfiguracaoSemSecao() => new ConfigurationBuilder().Build();

    private static IConfiguration ConfiguracaoCom(int diasMinimos) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CalendarioDiasUteis:AlertaHorizonteDiasMinimos"] = diasMinimos.ToString(),
            })
            .Build();

    private static IServiceScopeFactory FabricaDeEscoposCom(ICalendarioDiasUteisReadRepository repositorio)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repositorio);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task VerificarUmaVezAsync_SemConfiguracao_UsaOPadraoDeNoventaDiasParaAvaliarOHorizonte()
    {
        var metrics = new FakeBusinessMetrics();
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            () => Result<HorizonteCalendarioConsulta>.Success(
                new HorizonteCalendarioConsulta(new DateOnly(2026, 10, 12), new DateOnly(2026, 9, 12))));
        var guard = new CalendarioDiasUteisHorizonteGuard(
            FabricaDeEscoposCom(repositorio), metrics, ConfiguracaoSemSecao(), new FakeLogger<CalendarioDiasUteisHorizonteGuard>());

        await guard.VerificarUmaVezAsync(CancellationToken.None);

        Assert.Equal(CalendarioDiasUteisHorizonteGuard.DiasMinimosPadrao, Assert.Single(metrics.Registros).DiasMinimosConfigurados);
    }

    [Fact]
    public async Task VerificarUmaVezAsync_ComConfiguracao_UsaOValorConfiguradoEmVezDoPadrao()
    {
        var metrics = new FakeBusinessMetrics();
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            () => Result<HorizonteCalendarioConsulta>.Success(
                new HorizonteCalendarioConsulta(new DateOnly(2026, 10, 12), new DateOnly(2026, 9, 12))));
        var guard = new CalendarioDiasUteisHorizonteGuard(
            FabricaDeEscoposCom(repositorio), metrics, ConfiguracaoCom(30), new FakeLogger<CalendarioDiasUteisHorizonteGuard>());

        await guard.VerificarUmaVezAsync(CancellationToken.None);

        Assert.Equal(30, Assert.Single(metrics.Registros).DiasMinimosConfigurados);
    }

    [Fact]
    public async Task VerificarUmaVezAsync_ComHorizonteFolgado_RegistraDiasRestantesCorretosNaMetrica()
    {
        var metrics = new FakeBusinessMetrics();
        var hoje = new DateOnly(2026, 9, 12);
        var dataMaxima = new DateOnly(2030, 12, 31);
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            () => Result<HorizonteCalendarioConsulta>.Success(new HorizonteCalendarioConsulta(dataMaxima, hoje)));
        var guard = new CalendarioDiasUteisHorizonteGuard(
            FabricaDeEscoposCom(repositorio), metrics, ConfiguracaoSemSecao(), new FakeLogger<CalendarioDiasUteisHorizonteGuard>());

        await guard.VerificarUmaVezAsync(CancellationToken.None);

        Assert.Equal(dataMaxima.DayNumber - hoje.DayNumber, Assert.Single(metrics.Registros).DiasRestantes);
    }

    [Fact]
    public async Task VerificarUmaVezAsync_QuandoRepositorioFalha_NaoRegistraMetricaELogaWarning()
    {
        var metrics = new FakeBusinessMetrics();
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            () => Result<HorizonteCalendarioConsulta>.Failure(new Error("Infra.Indisponivel", "banco fora do ar", ErrorType.Unavailable)));
        var logger = new FakeLogger<CalendarioDiasUteisHorizonteGuard>();
        var guard = new CalendarioDiasUteisHorizonteGuard(FabricaDeEscoposCom(repositorio), metrics, ConfiguracaoSemSecao(), logger);

        await guard.VerificarUmaVezAsync(CancellationToken.None);

        Assert.Empty(metrics.Registros);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }
}

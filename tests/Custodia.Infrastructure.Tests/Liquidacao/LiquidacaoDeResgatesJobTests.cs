using Custodia.Application.Liquidacao;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Tests.Observability;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Tests.Liquidacao;

public sealed class LiquidacaoDeResgatesJobTests
{
    private static IConfiguration ConfiguracaoSemSecao() => new ConfigurationBuilder().Build();

    private static IServiceScopeFactory FabricaDeEscoposCom(Result<ResultadoLiquidacaoDeResgates> resultado)
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator>(_ => new FakeMediatorParaLiquidacaoDeResgates(resultado));
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ExecutarUmCicloAsync_ComDesfechoDeCompletude_LogaCompletudeENaoLogaAviso()
    {
        var resultado = Result<ResultadoLiquidacaoDeResgates>.Success(new ResultadoLiquidacaoDeResgates(
            DesfechoLiquidacaoDeResgates.Completude,
            CandidatasExaminadas: 3,
            FatosLiquidados: 2,
            FatosNaoVencidos: 1,
            FatosJaTratados: 0,
            FatosInconsistentes: 0));
        var logger = new FakeLogger<LiquidacaoDeResgatesJob>();
        var job = new LiquidacaoDeResgatesJob(FabricaDeEscoposCom(resultado), ConfiguracaoSemSecao(), logger);

        await job.ExecutarUmCicloAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("COMPLETUDE"));
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ExecutarUmCicloAsync_ComCandidataPulada_LogaAvisoComONumeroDeInconsistentesENaoSeDeclaraCompletude()
    {
        var resultado = Result<ResultadoLiquidacaoDeResgates>.Success(new ResultadoLiquidacaoDeResgates(
            DesfechoLiquidacaoDeResgates.ParcialPorInconsistencia,
            CandidatasExaminadas: 5,
            FatosLiquidados: 2,
            FatosNaoVencidos: 1,
            FatosJaTratados: 0,
            FatosInconsistentes: 2));
        var logger = new FakeLogger<LiquidacaoDeResgatesJob>();
        var job = new LiquidacaoDeResgatesJob(FabricaDeEscoposCom(resultado), ConfiguracaoSemSecao(), logger);

        await job.ExecutarUmCicloAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("2 inconsistentes"));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("concluído por COMPLETUDE"));
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Information);
    }
}

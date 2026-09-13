using Custodia.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prometheus;

namespace Custodia.Infrastructure.Tests.Observability;

public sealed class BusinessMetricsTests
{
    private static readonly Counter PosicaoNegativaSinalizadaTotal = Metrics.CreateCounter(
        "custodia_posicao_negativa_sinalizada_total", "help", new CounterConfiguration { LabelNames = ["instrumento_id"] });

    private static readonly Gauge CalendarioDiasUteisHorizonteDiasRestantesGauge = Metrics.CreateGauge(
        "custodia_calendario_dias_uteis_horizonte_dias_restantes", "help");

    private readonly BusinessMetrics _metrics = new(NullLogger<BusinessMetrics>.Instance);

    [Fact]
    public void RegistrarPosicaoNegativaSinalizada_IncrementaOContadorRotuladoPorInstrumento()
    {
        var instrumentoId = $"td:metrics-{Guid.NewGuid():N}";
        var antes = PosicaoNegativaSinalizadaTotal.WithLabels(instrumentoId).Value;

        _metrics.RegistrarPosicaoNegativaSinalizada("cli-001", instrumentoId, -5m);

        var depois = PosicaoNegativaSinalizadaTotal.WithLabels(instrumentoId).Value;
        Assert.Equal(1, depois - antes);
    }

    [Fact]
    public void RegistrarPosicaoNegativaSinalizada_NaoLancaComClienteIdDiferentesParaOMesmoInstrumento()
    {
        var instrumentoId = $"td:metrics-{Guid.NewGuid():N}";

        var exception = Record.Exception(() =>
        {
            _metrics.RegistrarPosicaoNegativaSinalizada("cli-001", instrumentoId, -5m);
            _metrics.RegistrarPosicaoNegativaSinalizada("cli-002", instrumentoId, -3m);
        });

        Assert.Null(exception);
        Assert.True(PosicaoNegativaSinalizadaTotal.WithLabels(instrumentoId).Value >= 2);
    }

    [Fact]
    public void RegistrarHorizonteCalendarioDiasUteis_SempreAtualizaOGaugeComOValorInformado()
    {
        _metrics.RegistrarHorizonteCalendarioDiasUteis(diasRestantes: 1234, diasMinimosConfigurados: 90);

        Assert.Equal(1234, CalendarioDiasUteisHorizonteDiasRestantesGauge.Value);
    }

    [Fact]
    public void RegistrarHorizonteCalendarioDiasUteis_QuandoHorizonteEstaCurto_DisparaAlertaEmLogDeWarning()
    {
        var logger = new FakeLogger<BusinessMetrics>();
        var metrics = new BusinessMetrics(logger);

        metrics.RegistrarHorizonteCalendarioDiasUteis(diasRestantes: 30, diasMinimosConfigurados: 90);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void RegistrarHorizonteCalendarioDiasUteis_QuandoHorizonteEstaFolgado_NaoDisparaAlertaAlgum()
    {
        var logger = new FakeLogger<BusinessMetrics>();
        var metrics = new BusinessMetrics(logger);

        metrics.RegistrarHorizonteCalendarioDiasUteis(diasRestantes: 1500, diasMinimosConfigurados: 90);

        Assert.Empty(logger.Entries);
    }
}

using Custodia.Infrastructure.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Prometheus;

namespace Custodia.Infrastructure.Tests.Observability;

public sealed class BusinessMetricsTests
{
    private static readonly Counter PosicaoNegativaSinalizadaTotal = Metrics.CreateCounter(
        "custodia_posicao_negativa_sinalizada_total", "help", new CounterConfiguration { LabelNames = ["instrumento_id"] });

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
}

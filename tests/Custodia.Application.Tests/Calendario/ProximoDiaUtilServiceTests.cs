using Custodia.Application.Calendario;
using Custodia.Application.Tests.Fakes;
using Custodia.Domain.Calendario;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Calendario;

public sealed class ProximoDiaUtilServiceTests
{
    private static readonly DateOnly UmaSexta = new(2024, 1, 5);
    private static readonly DateOnly UmaSegunda = new(2024, 1, 8);

    [Fact]
    public async Task ProximoDiaUtilAsync_QuandoRepositorioEncontra_DevolveSucessoComADataDoRepositorio()
    {
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            proximoDiaUtil: data => Result<ProximoDiaUtilConsulta>.Success(ProximoDiaUtilConsulta.De(UmaSegunda)));
        var servico = new ProximoDiaUtilService(repositorio);

        var resultado = await servico.ProximoDiaUtilAsync(UmaSexta, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(UmaSegunda, resultado.Value);
    }

    [Fact]
    public async Task ProximoDiaUtilAsync_QuandoRepositorioNaoEncontra_DevolveFalhaDeHorizonteEsgotado()
    {
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            proximoDiaUtil: _ => Result<ProximoDiaUtilConsulta>.Success(ProximoDiaUtilConsulta.NaoEncontrado));
        var servico = new ProximoDiaUtilService(repositorio);

        var resultado = await servico.ProximoDiaUtilAsync(new DateOnly(2030, 12, 31), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CalendarioDiasUteisErrors.HorizonteEsgotado, resultado.Error);
    }

    [Fact]
    public async Task ProximoDiaUtilAsync_QuandoRepositorioFalha_PropagaOErroDoRepositorioSemSubstituirPorHorizonteEsgotado()
    {
        var erroDeInfraestrutura = new Error("Infra.Indisponivel", "banco fora do ar", ErrorType.Unavailable);
        var repositorio = new FakeCalendarioDiasUteisReadRepository(
            proximoDiaUtil: _ => Result<ProximoDiaUtilConsulta>.Failure(erroDeInfraestrutura));
        var servico = new ProximoDiaUtilService(repositorio);

        var resultado = await servico.ProximoDiaUtilAsync(UmaSexta, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(erroDeInfraestrutura, resultado.Error);
        Assert.NotEqual(CalendarioDiasUteisErrors.HorizonteEsgotado, resultado.Error);
    }
}

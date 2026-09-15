using Custodia.Application.Eventos;
using Custodia.Application.Precos;
using Custodia.Application.Tests.Fakes;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Precos;

public sealed class ProcessarPriceObservedCommandHandlerTests
{
    private const string InstrumentoId = "td:tesouro-ipca-2035-05-15";

    private static PriceObservedEvento CriarEvento(
        string instrumentoId = InstrumentoId,
        DateOnly? dataRef = null,
        string campo = "pu_venda",
        decimal valor = 3496.412345m,
        string fonte = "td-api",
        int revisao = 0,
        DateTimeOffset? observadoEm = null) =>
        new(
            instrumentoId,
            dataRef ?? new DateOnly(2026, 8, 1),
            campo,
            valor,
            fonte,
            revisao,
            observadoEm ?? new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.Zero));

    private static (
        ProcessarPriceObservedCommandHandler Handler,
        FakePrecoWriteRepository PrecoWrite,
        FakeUnitOfWork UnitOfWork,
        FakeBusinessMetrics Metrics) CriarHandler(
        Func<ObservacaoDePreco, Result<ResultadoHistorico>>? registrarHistorico = null,
        Func<ObservacaoDePreco, Result<ResultadoAtualizacaoPrecoAtual>>? atualizarPrecoAtual = null,
        Func<ObservacaoDePreco, Result<ValorRevisaoAnteriorConsulta>>? obterValorRevisaoAnterior = null)
    {
        var precoWrite = new FakePrecoWriteRepository(registrarHistorico, atualizarPrecoAtual, obterValorRevisaoAnterior);
        var unitOfWork = new FakeUnitOfWork();
        var metrics = new FakeBusinessMetrics();
        var handler = new ProcessarPriceObservedCommandHandler(precoWrite, unitOfWork, metrics);

        return (handler, precoWrite, unitOfWork, metrics);
    }

    [Fact]
    public async Task Handle_HistoricoInseridoEPrecoAtualAtualizado_DevolveAplicadoEmPrecoAtual()
    {
        var (handler, precoWrite, unitOfWork, _) = CriarHandler();
        var evento = CriarEvento();

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoPriceObservedTipo.AplicadoEmPrecoAtual, resultado.Value.Tipo);
        Assert.Single(precoWrite.HistoricosRegistrados);
        Assert.Single(precoWrite.PrecosAtuaisAtualizados);
        Assert.Equal(1, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Handle_HistoricoInseridoMasPrecoAtualNaoAtualizado_DevolveSoHistoricoComOMotivo()
    {
        var (handler, _, _, _) = CriarHandler(
            atualizarPrecoAtual: _ => Result<ResultadoAtualizacaoPrecoAtual>.Success(
                ResultadoAtualizacaoPrecoAtual.NaoAtualizado(MotivoPrecoAtualNaoAtualizado.CampoDiferenteDoGravado)));
        var evento = CriarEvento();

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoPriceObservedTipo.SoHistorico, resultado.Value.Tipo);
        Assert.Equal(MotivoPrecoAtualNaoAtualizado.CampoDiferenteDoGravado, resultado.Value.MotivoSoHistorico);
    }

    [Fact]
    public async Task Handle_ReplayInocuo_DevolveReplaySemAlteracaoENaoTocaEmPrecoAtual()
    {
        var (handler, precoWrite, _, _) = CriarHandler(
            registrarHistorico: _ => Result<ResultadoHistorico>.Success(ResultadoHistorico.ReplayInocuo()));
        var evento = CriarEvento();

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoPriceObservedTipo.ReplaySemAlteracao, resultado.Value.Tipo);
        Assert.Empty(precoWrite.PrecosAtuaisAtualizados);
    }

    [Fact]
    public async Task Handle_ValorDivergente_DevolveValorDivergenteComOsDoisValoresENaoTocaEmPrecoAtual()
    {
        var (handler, precoWrite, _, metrics) = CriarHandler(
            registrarHistorico: _ => Result<ResultadoHistorico>.Success(ResultadoHistorico.Divergente(1000m)));
        var evento = CriarEvento(valor: 2000m);

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoPriceObservedTipo.ValorDivergente, resultado.Value.Tipo);
        Assert.Equal(1000m, resultado.Value.ValorAnteriorDivergente);
        Assert.Equal(2000m, resultado.Value.ValorNovoDivergente);
        Assert.Empty(precoWrite.PrecosAtuaisAtualizados);
        Assert.Single(metrics.ValoresDivergentesNoHistoricoDePrecos);
    }

    [Fact]
    public async Task Handle_RevisaoMaiorQueZero_RegistraAMetricaDeRevisao()
    {
        var (handler, _, _, metrics) = CriarHandler(
            obterValorRevisaoAnterior: _ => Result<ValorRevisaoAnteriorConsulta>.Success(ValorRevisaoAnteriorConsulta.De(1000m)));
        var evento = CriarEvento(revisao: 1, valor: 1100m);

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var registro = Assert.Single(metrics.RevisoesDePrecoRecebidas);
        Assert.Equal(1, registro.Revisao);
        Assert.Equal(1100m, registro.ValorNovo);
        Assert.Equal(1000m, registro.ValorAnterior);
    }

    [Fact]
    public async Task Handle_RevisaoZero_NaoRegistraAMetricaDeRevisao_Controle()
    {
        var (handler, _, _, metrics) = CriarHandler();
        var evento = CriarEvento(revisao: 0);

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(metrics.RevisoesDePrecoRecebidas);
    }

    [Fact]
    public async Task Handle_FalhaAoRegistrarHistorico_PropagaOErroSemChamarSaveChanges()
    {
        var erro = new Error("Preco.FalhaSimulada", "falha simulada", ErrorType.Unprocessable);
        var (handler, _, unitOfWork, _) = CriarHandler(
            registrarHistorico: _ => Result<ResultadoHistorico>.Failure(erro));
        var evento = CriarEvento();

        var resultado = await handler.Handle(new ProcessarPriceObservedCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(erro, resultado.Error);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }
}

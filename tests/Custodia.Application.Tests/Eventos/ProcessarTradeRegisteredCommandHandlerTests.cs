using System.Reflection;
using Custodia.Application.Eventos;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Application.Tests.Fakes;
using Custodia.Domain.Common;
using Custodia.Domain.Eventos;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Tests.Eventos;

public sealed class ProcessarTradeRegisteredCommandHandlerTests
{
    private const string ClienteId = "cli-001";
    private const string InstrumentoId = "td:tesouro-ipca-2035-05-15";

    private static DateOnly Dia(int offset) => new DateOnly(2026, 8, 1).AddDays(offset);

    private static DateTimeOffset Instante(int offset) =>
        new DateTimeOffset(2026, 8, 15, 14, 0, 0, TimeSpan.Zero).AddMinutes(offset);

    private static TradeRegisteredEvento CriarEvento(
        string tradeId = "op-7f3a",
        string clienteId = ClienteId,
        string instrumentoId = InstrumentoId,
        OperacaoTrade operacao = OperacaoTrade.Aplicacao,
        decimal quantidade = 10m,
        decimal valorFinanceiro = 1000m,
        int diaEvento = 0,
        int minutoRegistro = 0,
        string? estornaTradeId = null,
        string? valorOrigemSaldoBruto = null) =>
        new(
            tradeId,
            clienteId,
            instrumentoId,
            operacao,
            quantidade,
            valorFinanceiro,
            Dia(diaEvento),
            Instante(minutoRegistro),
            estornaTradeId,
            valorOrigemSaldoBruto);

    private static (
        ProcessarTradeRegisteredCommandHandler Handler,
        FakeMovimentoWriteRepository MovimentoWrite,
        FakePosicaoCorrenteWriteRepository PosicaoWrite,
        FakeUnitOfWork UnitOfWork,
        FakeBusinessMetrics Metrics) CriarHandler(
        IReadOnlyList<Movimento>? movimentosExistentes = null,
        IReadOnlyDictionary<(string, string), PosicaoTresColunas>? posicoesExistentes = null,
        Func<Movimento, Result>? adicionarMovimento = null,
        Func<int, Result>? saveChanges = null)
    {
        var movimentoRead = new FakeMovimentoReadRepository(movimentosExistentes ?? []);
        var movimentoWrite = new FakeMovimentoWriteRepository(adicionarMovimento);
        var posicaoRead = new FakePosicaoCorrenteReadRepository(posicoesExistentes);
        var posicaoWrite = new FakePosicaoCorrenteWriteRepository();
        var unitOfWork = new FakeUnitOfWork(saveChanges);
        var metrics = new FakeBusinessMetrics();

        var handler = new ProcessarTradeRegisteredCommandHandler(
            movimentoRead, movimentoWrite, posicaoRead, posicaoWrite, unitOfWork, metrics);

        return (handler, movimentoWrite, posicaoWrite, unitOfWork, metrics);
    }

    [Fact]
    public async Task Handle_Aplicacao_MapeiaParaCompra_ComInstrumentoDoEventoEQuantidadeComSinalPositivo()
    {
        var (handler, movimentoWrite, posicaoWrite, unitOfWork, _) = CriarHandler();
        var evento = CriarEvento(operacao: OperacaoTrade.Aplicacao, quantidade: 10m, valorFinanceiro: 1000m, valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        var gravado = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(TipoMovimento.Compra, gravado.Tipo);
        Assert.Equal(InstrumentoId, gravado.InstrumentoId);
        Assert.Equal(10m, gravado.QtdDelta);
        Assert.Equal(1000m, gravado.ValorFinanceiro);
        Assert.Equal("op-7f3a", gravado.RefExterna);
        Assert.Equal(1, unitOfWork.ChamadasDeSaveChanges);
        Assert.NotNull(posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentoId));
    }

    [Fact]
    public async Task Handle_Resgate_MapeiaParaVenda_NuncaParaOEnumResgateDoLivro()
    {
        var (handler, movimentoWrite, _, _, _) = CriarHandler();
        var evento = CriarEvento(operacao: OperacaoTrade.Resgate, quantidade: 4m, valorFinanceiro: 600m);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var gravado = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(TipoMovimento.Venda, gravado.Tipo);
        Assert.NotEqual(TipoMovimento.Resgate, gravado.Tipo);
        Assert.Equal(-4m, gravado.QtdDelta);
        Assert.Equal(600m, gravado.ValorFinanceiro);
    }

    [Fact]
    public async Task Handle_Aporte_GravaInstrumentoEQuantidadeDoEvento_NuncaCaixaBrlNemValorOrigemSaldo()
    {
        var (handler, movimentoWrite, _, _, _) = CriarHandler();
        var evento = CriarEvento(
            operacao: OperacaoTrade.Aporte, quantidade: 7m, valorFinanceiro: 700m, valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var gravado = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(TipoMovimento.Aporte, gravado.Tipo);
        Assert.Equal(InstrumentoId, gravado.InstrumentoId);
        Assert.NotEqual(InstrumentosCaixa.Brl, gravado.InstrumentoId);
        Assert.Equal(7m, gravado.QtdDelta);
        Assert.Equal(700m, gravado.ValorFinanceiro);
    }

    [Theory]
    [InlineData(OperacaoTrade.Aplicacao)]
    [InlineData(OperacaoTrade.Aporte)]
    public async Task Handle_ValorOrigemSaldoAusente_EstacionaComOrigemRecursoAusente_NadaEhGravado(OperacaoTrade operacao)
    {
        var (handler, movimentoWrite, _, unitOfWork, _) = CriarHandler();
        var evento = CriarEvento(operacao: operacao, valorOrigemSaldoBruto: null);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.OrigemRecursoAusente, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }

    [Theory]
    [InlineData("não-decimal")]
    [InlineData("-1.00")]
    [InlineData("1000.01")]
    public async Task Handle_ValorOrigemSaldoInvalido_EstacionaComOrigemRecursoInvalida_NadaEhGravado(string valorOrigemSaldo)
    {
        var (handler, movimentoWrite, _, _, _) = CriarHandler();
        var evento = CriarEvento(
            operacao: OperacaoTrade.Aplicacao, valorFinanceiro: 1000m, valorOrigemSaldoBruto: valorOrigemSaldo);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.OrigemRecursoInvalida, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_ValorOrigemSaldoIgualAZero_GravaApenasALinhaDoTitulo()
    {
        var (handler, movimentoWrite, _, _, _) = CriarHandler();
        var evento = CriarEvento(valorFinanceiro: 1000m, valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var gravado = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(InstrumentoId, gravado.InstrumentoId);
    }

    [Fact]
    public async Task Handle_ValorOrigemSaldoMisto_GravaDuasLinhas_ComPernaDeCaixaComSinalNegativoDoOrigem()
    {
        var (handler, movimentoWrite, posicaoWrite, _, _) = CriarHandler();
        var evento = CriarEvento(
            tradeId: "op-misto", valorFinanceiro: 1000m, quantidade: 10m, valorOrigemSaldoBruto: "400.00");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, movimentoWrite.Adicionados.Count);

        var titulo = movimentoWrite.Adicionados.Single(m => m.InstrumentoId == InstrumentoId);
        Assert.Equal(10m, titulo.QtdDelta);
        Assert.Equal(1000m, titulo.ValorFinanceiro);
        Assert.Equal("op-misto", titulo.RefExterna);

        var perna = movimentoWrite.Adicionados.Single(m => m.InstrumentoId == InstrumentosCaixa.Brl);
        Assert.Equal(TipoMovimento.Compra, perna.Tipo);
        Assert.Equal(-400.00m, perna.QtdDelta);
        Assert.Equal(400.00m, perna.ValorFinanceiro);
        Assert.Equal("op-misto:brl", perna.RefExterna);

        Assert.NotNull(posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentosCaixa.Brl));
    }

    [Fact]
    public async Task Handle_ReentregaDaMesmaRefExterna_EhNoOpEDevolveEscrituradoComReplay()
    {
        var existente = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, unitOfWork, _) = CriarHandler(movimentosExistentes: [existente]);
        var evento = CriarEvento(tradeId: "op-7f3a", valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.True(resultado.Value.Replay);
        Assert.Empty(movimentoWrite.Adicionados);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Handle_DecimalForaDaEscala_EstacionaComPayloadInvalido()
    {
        var (handler, movimentoWrite, _, _, _) = CriarHandler();
        var evento = CriarEvento(quantidade: 9_999_999_999_999_999_999m, valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.PayloadInvalido, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_EstornoComTituloNoMesmoCliente_GravaAjusteSimetrico()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, posicaoWrite, _, _) = CriarHandler(movimentosExistentes: [titulo]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            diaEvento: 5,
            estornaTradeId: "op-7f3a");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        var ajuste = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(TipoMovimento.Ajuste, ajuste.Tipo);
        Assert.Equal(InstrumentoId, ajuste.InstrumentoId);
        Assert.Equal(-10m, ajuste.QtdDelta);
        Assert.Equal(-1000m, ajuste.ValorFinanceiro);
        Assert.Equal("op-estorno-1", ajuste.RefExterna);
        Assert.Equal(titulo.Id, ajuste.RefEstorno);
        Assert.Equal(titulo.DataEvento, ajuste.DataEvento);
        Assert.NotNull(posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentoId));
    }

    [Fact]
    public async Task Handle_EstornoComTituloEmOutroCliente_EstacionaComEstornoClienteDivergente_SemRetry()
    {
        var tituloDeOutroCliente = MovimentoTestFactory.Criar(
            1, "cli-999", InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _) = CriarHandler(movimentosExistentes: [tituloDeOutroCliente]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-7f3a");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.EstornoClienteDivergente, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_EstornoOrfaoEmNenhumCliente_DevolveEnviarParaRetry_NadaEhGravado()
    {
        var (handler, movimentoWrite, _, _, _) = CriarHandler();
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-nunca-chegou");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.EnviarParaRetry, resultado.Value.Tipo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Theory]
    [InlineData("td:outro-instrumento", 10, 1000)]
    [InlineData("td:tesouro-ipca-2035-05-15", 5, 1000)]
    [InlineData("td:tesouro-ipca-2035-05-15", 10, 500)]
    public async Task Handle_EstornoComCampoDivergenteDoTitulo_EstacionaComEstornoDivergente_NadaEhGravado(
        string instrumentoIdDoEstorno, decimal quantidadeDoEstorno, decimal valorFinanceiroDoEstorno)
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            instrumentoId: instrumentoIdDoEstorno,
            quantidade: quantidadeDoEstorno,
            valorFinanceiro: valorFinanceiroDoEstorno,
            estornaTradeId: "op-7f3a");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.EstornoDivergente, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_EstornoComTresCamposBatendo_ControlePositivo_GravaAjusteNormalmente()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            instrumentoId: InstrumentoId,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-7f3a");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.Single(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_EstornoDeTradeComPerna_GravaDoisAjustes_ComRefExternaERefEstornoCorretos()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-misto");

        var perna = MovimentoTestFactory.Criar(
            2, ClienteId, InstrumentosCaixa.Brl, TipoMovimento.Compra, Dia(0), Instante(0), -400m, 400m, "op-misto:brl");

        var (handler, movimentoWrite, _, _, _) = CriarHandler(movimentosExistentes: [titulo, perna]);
        var evento = CriarEvento(
            tradeId: "op-estorno-misto",
            operacao: OperacaoTrade.Estorno,
            instrumentoId: InstrumentoId,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            diaEvento: 3,
            estornaTradeId: "op-misto");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, movimentoWrite.Adicionados.Count);

        var ajusteTitulo = movimentoWrite.Adicionados.Single(m => m.InstrumentoId == InstrumentoId);
        Assert.Equal("op-estorno-misto", ajusteTitulo.RefExterna);
        Assert.Equal(titulo.Id, ajusteTitulo.RefEstorno);
        Assert.Equal(-10m, ajusteTitulo.QtdDelta);
        Assert.Equal(-1000m, ajusteTitulo.ValorFinanceiro);

        var ajustePerna = movimentoWrite.Adicionados.Single(m => m.InstrumentoId == InstrumentosCaixa.Brl);
        Assert.Equal("op-estorno-misto:brl", ajustePerna.RefExterna);
        Assert.Equal(perna.Id, ajustePerna.RefEstorno);
        Assert.Equal(400m, ajustePerna.QtdDelta);
        Assert.Equal(-400m, ajustePerna.ValorFinanceiro);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorRefEstornoDuplicado_EstacionaComEstornoDuplicado_ComoSucesso()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, _, _, _, _) = CriarHandler(
            movimentosExistentes: [titulo],
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.RefEstornoDuplicado));

        var evento = CriarEvento(
            tradeId: "op-estorno-2",
            operacao: OperacaoTrade.Estorno,
            instrumentoId: InstrumentoId,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-7f3a");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.EstornoDuplicado, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorIdentificadorComEspacoNaBorda_Estaciona_ComoSucesso()
    {
        var (handler, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.IdentificadorComEspacoNaBorda));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.IdentificadorComEspacoNaBorda, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaGenuinaDeGravacaoNaoClassificada_PropagaComoFalhaDeResult_NaoViraMotivo()
    {
        var erroGenerico = new Error("Infra.Indisponivel", "banco fora do ar", ErrorType.Unavailable);
        var (handler, _, _, _, _) = CriarHandler(saveChanges: _ => Result.Failure(erroGenerico));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(erroGenerico, resultado.Error);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorMensagemDuplicada_TraduzParaEscrituradoComReplay_PoisEhReentregaDaMesmaMensagemEDedupeFuncionou()
    {
        var (handler, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.MensagemDuplicada));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.True(resultado.Value.Replay);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorValorNumericoExcedeMagnitudeOuEscala_EstacionaComPayloadInvalido()
    {
        var (handler, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.PayloadInvalido, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorDataEventoFutura_EstacionaComPayloadInvalido_PoisOperacoesJaDeveriaTerRejeitadoNaBorda()
    {
        var (handler, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.DataEventoFutura));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoEstacionamento.PayloadInvalido, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorOperacaoNaoPermitidaSobreMovimentoImutavel_PropagaComoFalhaAltaEVisivel_PoisEhInalcancavelDesteHandlerQueSoFazInsertESeAparecerEhDefeitoNosso()
    {
        var erro = MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel;
        var (handler, _, _, _, _) = CriarHandler(saveChanges: _ => Result.Failure(erro));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(erro, resultado.Error);
    }

    private static readonly IReadOnlyDictionary<string, Action<Result<ResultadoTradeRegistered>, Error>>
        DesfechoEsperadoPorCampoDeMovimentoWriteErrors = new Dictionary<string, Action<Result<ResultadoTradeRegistered>, Error>>
        {
            [nameof(MovimentoWriteErrors.RefEstornoDuplicado)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
                Assert.Equal(MotivoEstacionamento.EstornoDuplicado, resultado.Value.Motivo);
            },
            [nameof(MovimentoWriteErrors.IdentificadorComEspacoNaBorda)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
                Assert.Equal(MotivoEstacionamento.IdentificadorComEspacoNaBorda, resultado.Value.Motivo);
            },
            [nameof(MovimentoWriteErrors.MensagemDuplicada)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
                Assert.True(resultado.Value.Replay);
            },
            [nameof(MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
                Assert.Equal(MotivoEstacionamento.PayloadInvalido, resultado.Value.Motivo);
            },
            [nameof(MovimentoWriteErrors.DataEventoFutura)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
                Assert.Equal(MotivoEstacionamento.PayloadInvalido, resultado.Value.Motivo);
            },
            [nameof(MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel)] = (resultado, erro) =>
            {
                Assert.True(resultado.IsFailure);
                Assert.Equal(erro, resultado.Error);
            },
        };

    [Fact]
    public async Task ClassificarFalhaDeGravacao_TodoErroPublicoDeMovimentoWriteErrors_TemDesfechoExplicitamenteClassificado_NuncaOFallbackGenerico()
    {
        var camposDeErro = typeof(MovimentoWriteErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(campo => campo.FieldType == typeof(Error))
            .ToList();

        Assert.NotEmpty(camposDeErro);

        foreach (var campo in camposDeErro)
        {
            Assert.True(
                DesfechoEsperadoPorCampoDeMovimentoWriteErrors.ContainsKey(campo.Name),
                $"MovimentoWriteErrors.{campo.Name} não tem desfecho esperado registrado nesta guarda de " +
                "totalidade — classifique-o em ClassificarFalhaDeGravacao e registre o desfecho aqui antes " +
                "de liberar este Error.");

            var erro = (Error)campo.GetValue(null)!;
            var (handler, _, _, _, _) = CriarHandler(saveChanges: _ => Result.Failure(erro));
            var evento = CriarEvento(valorOrigemSaldoBruto: "0");

            var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

            DesfechoEsperadoPorCampoDeMovimentoWriteErrors[campo.Name](resultado, erro);
        }
    }

    [Fact]
    public async Task Handle_ResgateMaiorQuePosicaoExistente_GravaMesmoAssimESinaliza()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-compra-1");

        var posicoes = new Dictionary<(string, string), PosicaoTresColunas>
        {
            [(ClienteId, InstrumentoId)] = new PosicaoTresColunas(10m, 1000m, 100m),
        };

        var (handler, movimentoWrite, _, _, metrics) = CriarHandler(
            movimentosExistentes: [compra], posicoesExistentes: posicoes);

        var evento = CriarEvento(
            tradeId: "op-resgate-grande",
            operacao: OperacaoTrade.Resgate,
            quantidade: 25m,
            valorFinanceiro: 2500m,
            diaEvento: 1);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.Single(movimentoWrite.Adicionados);

        var sinalizacao = Assert.Single(metrics.Sinalizacoes);
        Assert.Equal(ClienteId, sinalizacao.ClienteId);
        Assert.Equal(InstrumentoId, sinalizacao.InstrumentoId);
        Assert.True(sinalizacao.QuantidadeResultante < 0m);
    }

    [Fact]
    public async Task Handle_AplicacaoNormal_NaoSinalizaNada()
    {
        var (handler, _, _, _, metrics) = CriarHandler();
        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.Empty(metrics.Sinalizacoes);
    }

    [Fact]
    public async Task Handle_SomaDeCaixaBrlComALiquidarFicaNegativa_Sinaliza_LeituraPelaSomaNuncaPorLinha()
    {
        var posicoes = new Dictionary<(string, string), PosicaoTresColunas>
        {
            [(ClienteId, InstrumentosCaixa.ALiquidar)] = new PosicaoTresColunas(200m, 200m, 1.000000m),
        };

        var (handler, _, _, _, metrics) = CriarHandler(posicoesExistentes: posicoes);
        var evento = CriarEvento(valorFinanceiro: 1000m, quantidade: 10m, valorOrigemSaldoBruto: "500.00");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var sinalizacao = Assert.Single(metrics.Sinalizacoes);
        Assert.Equal(InstrumentosCaixa.Brl, sinalizacao.InstrumentoId);
        Assert.Equal(-300m, sinalizacao.QuantidadeResultante);
    }

    [Fact]
    public async Task Handle_SomaDeCaixaBrlComALiquidarPermaneceNaoNegativa_NaoSinaliza()
    {
        var posicoes = new Dictionary<(string, string), PosicaoTresColunas>
        {
            [(ClienteId, InstrumentosCaixa.ALiquidar)] = new PosicaoTresColunas(900m, 900m, 1.000000m),
        };

        var (handler, _, _, _, metrics) = CriarHandler(posicoesExistentes: posicoes);
        var evento = CriarEvento(valorFinanceiro: 1000m, quantidade: 10m, valorOrigemSaldoBruto: "900.00");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(metrics.Sinalizacoes);
    }

    [Fact]
    public async Task Handle_MovimentoRetroativo_ReDobraAChaveInteira_EmVezDeAplicarDelta()
    {
        var compraD1 = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(1), Instante(1), 10m, 1000m, "op-1");
        var vendaD2 = MovimentoTestFactory.Criar(
            2, ClienteId, InstrumentoId, TipoMovimento.Venda, Dia(2), Instante(2), -4m, 600m, "op-2");

        var posicoes = new Dictionary<(string, string), PosicaoTresColunas>
        {
            [(ClienteId, InstrumentoId)] = new PosicaoTresColunas(6m, 600m, 100m),
        };

        var (handler, _, posicaoWrite, _, _) = CriarHandler(
            movimentosExistentes: [compraD1, vendaD2], posicoesExistentes: posicoes);

        var evento = CriarEvento(
            tradeId: "op-retroativo",
            operacao: OperacaoTrade.Aplicacao,
            quantidade: 10m,
            valorFinanceiro: 2000m,
            diaEvento: 0,
            valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var posicaoFinal = posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentoId);
        Assert.NotNull(posicaoFinal);
        Assert.Equal(2400m, posicaoFinal!.CustoTotal);
        Assert.Equal(150m, posicaoFinal.PrecoMedio);
        Assert.NotEqual(2600m, posicaoFinal.CustoTotal);
    }

    [Fact]
    public void TradeRegisteredEvento_TodosOsCamposEstaoMapeadosOuDispensadosPorRegraEscrita()
    {
        var propriedades = typeof(TradeRegisteredEvento).GetProperties().Select(p => p.Name).ToHashSet();

        var camposDeFato = new HashSet<string>
        {
            nameof(TradeRegisteredEvento.TradeId),
            nameof(TradeRegisteredEvento.ClienteId),
            nameof(TradeRegisteredEvento.InstrumentoId),
            nameof(TradeRegisteredEvento.Operacao),
            nameof(TradeRegisteredEvento.Quantidade),
            nameof(TradeRegisteredEvento.ValorFinanceiro),
            nameof(TradeRegisteredEvento.DataEvento),
            nameof(TradeRegisteredEvento.RegistradoEm),
        };

        var camposUsadosParaLookupOuFormacaoDaSegundaLinha = new HashSet<string>
        {
            nameof(TradeRegisteredEvento.EstornaTradeId),
            nameof(TradeRegisteredEvento.ValorOrigemSaldoBruto),
        };

        var todosOsCamposContabilizados = camposDeFato
            .Union(camposUsadosParaLookupOuFormacaoDaSegundaLinha)
            .ToHashSet();

        Assert.Equal(todosOsCamposContabilizados, propriedades);
    }

    [Fact]
    public async Task Handle_EstornoComConferenciaBatendo_InstrumentoIdQuantidadeEValorFinanceiroDoEventoSaoConferidosENaoGravados()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            instrumentoId: InstrumentoId,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-7f3a");

        await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        var ajuste = Assert.Single(movimentoWrite.Adicionados);
        Assert.NotEqual(evento.InstrumentoId, ajuste.RefExterna);
        Assert.NotEqual(evento.Quantidade, ajuste.QtdDelta);
    }
}

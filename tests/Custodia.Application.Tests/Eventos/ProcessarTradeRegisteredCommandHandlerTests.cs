using System.Reflection;
using System.Text.Json;
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
        FakeBusinessMetrics Metrics,
        FakeMovimentoTravamentoRepository MovimentoTravamento) CriarHandler(
        IReadOnlyList<Movimento>? movimentosExistentes = null,
        IReadOnlyDictionary<(string, string), PosicaoTresColunas>? posicoesExistentes = null,
        Func<Movimento, Result>? adicionarMovimento = null,
        Func<int, Result>? saveChanges = null)
    {
        var movimentos = movimentosExistentes ?? [];
        var movimentoRead = new FakeMovimentoReadRepository(movimentos);
        var movimentoWrite = new FakeMovimentoWriteRepository(adicionarMovimento);
        var movimentoTravamento = new FakeMovimentoTravamentoRepository(movimentos);
        var posicaoRead = new FakePosicaoCorrenteReadRepository(posicoesExistentes);
        var posicaoWrite = new FakePosicaoCorrenteWriteRepository();
        var unitOfWork = new FakeUnitOfWork(saveChanges);
        var metrics = new FakeBusinessMetrics();

        var aplicadorIncrementalDePosicao = new AplicadorIncrementalDePosicao(movimentoRead, posicaoRead);

        var handler = new ProcessarTradeRegisteredCommandHandler(
            movimentoRead, movimentoWrite, movimentoTravamento, posicaoRead, posicaoWrite,
            aplicadorIncrementalDePosicao, unitOfWork, metrics, new FakePausaEntreLerEGravar());

        return (handler, movimentoWrite, posicaoWrite, unitOfWork, metrics, movimentoTravamento);
    }

    [Fact]
    public async Task Handle_Aplicacao_MapeiaParaCompra_ComInstrumentoDoEventoEQuantidadeComSinalPositivo()
    {
        var (handler, movimentoWrite, posicaoWrite, unitOfWork, _, _) = CriarHandler();
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
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler();
        var evento = CriarEvento(operacao: OperacaoTrade.Resgate, quantidade: 4m, valorFinanceiro: 600m);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var gravado = movimentoWrite.Adicionados.Single(m => m.RefExterna == evento.TradeId);
        Assert.Equal(TipoMovimento.Venda, gravado.Tipo);
        Assert.NotEqual(TipoMovimento.Resgate, gravado.Tipo);
        Assert.Equal(-4m, gravado.QtdDelta);
        Assert.Equal(600m, gravado.ValorFinanceiro);
    }

    [Fact]
    public async Task Handle_Aporte_GravaInstrumentoEQuantidadeDoEvento_NuncaCaixaBrlNemValorOrigemSaldo()
    {
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler();
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
        var (handler, movimentoWrite, _, unitOfWork, _, _) = CriarHandler();
        var evento = CriarEvento(operacao: operacao, valorOrigemSaldoBruto: null);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.OrigemRecursoAusente, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }

    [Theory]
    [InlineData("não-decimal")]
    [InlineData("-1.00")]
    [InlineData("1000.01")]
    public async Task Handle_ValorOrigemSaldoInvalido_EstacionaComOrigemRecursoInvalida_NadaEhGravado(string valorOrigemSaldo)
    {
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler();
        var evento = CriarEvento(
            operacao: OperacaoTrade.Aplicacao, valorFinanceiro: 1000m, valorOrigemSaldoBruto: valorOrigemSaldo);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.OrigemRecursoInvalida, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_ValorOrigemSaldoIgualAZero_GravaApenasALinhaDoTitulo()
    {
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler();
        var evento = CriarEvento(valorFinanceiro: 1000m, valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var gravado = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(InstrumentoId, gravado.InstrumentoId);
    }

    [Fact]
    public async Task Handle_ValorOrigemSaldoMisto_GravaDuasLinhas_ComPernaDeCaixaComSinalNegativoDoOrigem()
    {
        var (handler, movimentoWrite, posicaoWrite, _, _, _) = CriarHandler();
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

        var (handler, movimentoWrite, _, unitOfWork, _, _) = CriarHandler(movimentosExistentes: [existente]);
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
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler();
        var evento = CriarEvento(quantidade: 9_999_999_999_999_999_999m, valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.PayloadInvalido, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_EstornoComTituloNoMesmoCliente_GravaAjusteSimetrico()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, posicaoWrite, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
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

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [tituloDeOutroCliente]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-7f3a");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.EstornoClienteDivergente, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_EstornoOrfaoEmNenhumCliente_DevolveEnviarParaRetry_NadaEhGravado()
    {
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler();
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

    [Fact]
    public async Task Handle_EstornoComTresCamposBatendo_ControlePositivo_GravaAjusteNormalmente()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
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

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [titulo, perna]);
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

        var (handler, _, _, _, _, _) = CriarHandler(
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
        Assert.Equal(MotivoParking.EstornoDuplicado, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorIdentificadorComEspacoNaBorda_Estaciona_ComoSucesso()
    {
        var (handler, _, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.IdentificadorComEspacoNaBorda));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.IdentificadorComEspacoNaBorda, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaGenuinaDeGravacaoNaoClassificada_PropagaComoFalhaDeResult_NaoViraMotivo()
    {
        var erroGenerico = new Error("Infra.Indisponivel", "banco fora do ar", ErrorType.Unavailable);
        var (handler, _, _, _, _, _) = CriarHandler(saveChanges: _ => Result.Failure(erroGenerico));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(erroGenerico, resultado.Error);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorMensagemDuplicada_TraduzParaEscrituradoComReplay_PoisEhReentregaDaMesmaMensagemEDedupeFuncionou()
    {
        var (handler, _, _, _, _, _) = CriarHandler(
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
        var (handler, _, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.PayloadInvalido, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorDataEventoFutura_EstacionaComPayloadInvalido_PoisOperacoesJaDeveriaTerRejeitadoNaBorda()
    {
        var (handler, _, _, _, _, _) = CriarHandler(
            saveChanges: _ => Result.Failure(MovimentoWriteErrors.DataEventoFutura));

        var evento = CriarEvento(valorOrigemSaldoBruto: "0");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
        Assert.Equal(MotivoParking.PayloadInvalido, resultado.Value.Motivo);
    }

    [Fact]
    public async Task Handle_FalhaDeGravacaoPorOperacaoNaoPermitidaSobreMovimentoImutavel_PropagaComoFalhaAltaEVisivel_PoisEhInalcancavelDesteHandlerQueSoFazInsertESeAparecerEhDefeitoNosso()
    {
        var erro = MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel;
        var (handler, _, _, _, _, _) = CriarHandler(saveChanges: _ => Result.Failure(erro));

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
                Assert.Equal(MotivoParking.EstornoDuplicado, resultado.Value.Motivo);
            },
            [nameof(MovimentoWriteErrors.IdentificadorComEspacoNaBorda)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
                Assert.Equal(MotivoParking.IdentificadorComEspacoNaBorda, resultado.Value.Motivo);
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
                Assert.Equal(MotivoParking.PayloadInvalido, resultado.Value.Motivo);
            },
            [nameof(MovimentoWriteErrors.DataEventoFutura)] = (resultado, _) =>
            {
                Assert.True(resultado.IsSuccess);
                Assert.Equal(ResultadoTradeRegisteredTipo.Estacionar, resultado.Value.Tipo);
                Assert.Equal(MotivoParking.PayloadInvalido, resultado.Value.Motivo);
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
            var (handler, _, _, _, _, _) = CriarHandler(saveChanges: _ => Result.Failure(erro));
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

        var (handler, movimentoWrite, _, _, metrics, _) = CriarHandler(
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
        Assert.Equal(2, movimentoWrite.Adicionados.Count);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.Tipo == TipoMovimento.IrRetido);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.Tipo == TipoMovimento.Iof);
        var aliq = movimentoWrite.Adicionados.Single(m => m.Tipo == TipoMovimento.ALiquidar);
        Assert.Equal(2500m, aliq.QtdDelta);

        var sinalizacao = Assert.Single(metrics.Sinalizacoes);
        Assert.Equal(ClienteId, sinalizacao.ClienteId);
        Assert.Equal(InstrumentoId, sinalizacao.InstrumentoId);
        Assert.True(sinalizacao.QuantidadeResultante < 0m);

        var sinalizacaoDeFilaInsuficiente = Assert.Single(metrics.SinalizacoesDeResgateSobrePrecoMedioProvisorio);
        Assert.Equal(ClienteId, sinalizacaoDeFilaInsuficiente.ClienteId);
        Assert.Equal(InstrumentoId, sinalizacaoDeFilaInsuficiente.InstrumentoId);
        Assert.Equal(15m, sinalizacaoDeFilaInsuficiente.QuantidadeDescoberta);
    }

    [Fact]
    public async Task Handle_AplicacaoNormal_NaoSinalizaNada()
    {
        var (handler, _, _, _, metrics, _) = CriarHandler();
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

        var (handler, _, _, _, metrics, _) = CriarHandler(posicoesExistentes: posicoes);
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

        var (handler, _, _, _, metrics, _) = CriarHandler(posicoesExistentes: posicoes);
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

        var (handler, _, posicaoWrite, _, _, _) = CriarHandler(
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

    private enum ClassificacaoDeCampoDoEnvelope
    {
        ColunaOuCampoQueCarregaAdiante,
        UsadoSemVirarColuna,
        DispensaDeclaradaNoEstorno,
    }

    private const string PayloadTradeRegisteredComTodosOsCamposDoEnvelope = """
        {
          "v": 1,
          "tipo": "TradeRegistered",
          "tradeId": "op-7f3a",
          "clienteId": "cli-001",
          "instrumentoId": "td:tesouro-ipca-2035-05-15",
          "operacao": "aplicacao",
          "quantidade": "2.86000000",
          "valorFinanceiro": "10000.00",
          "dataEvento": "2026-08-01",
          "registradoEm": "2026-08-15T14:02:11Z",
          "estornaTradeId": "op-3c9b",
          "valorOrigemSaldo": "900.00"
        }
        """;

    private static readonly IReadOnlyDictionary<string, ClassificacaoDeCampoDoEnvelope> ClassificacaoDosCamposDoEnvelopeTradeRegistered =
        new Dictionary<string, ClassificacaoDeCampoDoEnvelope>
        {
            ["v"] = ClassificacaoDeCampoDoEnvelope.UsadoSemVirarColuna,
            ["tipo"] = ClassificacaoDeCampoDoEnvelope.UsadoSemVirarColuna,
            ["tradeId"] = ClassificacaoDeCampoDoEnvelope.ColunaOuCampoQueCarregaAdiante,
            ["clienteId"] = ClassificacaoDeCampoDoEnvelope.ColunaOuCampoQueCarregaAdiante,
            ["instrumentoId"] = ClassificacaoDeCampoDoEnvelope.DispensaDeclaradaNoEstorno,
            ["operacao"] = ClassificacaoDeCampoDoEnvelope.ColunaOuCampoQueCarregaAdiante,
            ["quantidade"] = ClassificacaoDeCampoDoEnvelope.DispensaDeclaradaNoEstorno,
            ["valorFinanceiro"] = ClassificacaoDeCampoDoEnvelope.DispensaDeclaradaNoEstorno,
            ["dataEvento"] = ClassificacaoDeCampoDoEnvelope.ColunaOuCampoQueCarregaAdiante,
            ["registradoEm"] = ClassificacaoDeCampoDoEnvelope.ColunaOuCampoQueCarregaAdiante,
            ["estornaTradeId"] = ClassificacaoDeCampoDoEnvelope.ColunaOuCampoQueCarregaAdiante,
            ["valorOrigemSaldo"] = ClassificacaoDeCampoDoEnvelope.UsadoSemVirarColuna,
        };

    [Fact]
    public void CamposDoEnvelopeTradeRegistered_TodoCampoTemClassificacaoUnicaEExaustiva()
    {
        var camposDoEnvelope = JsonDocument.Parse(PayloadTradeRegisteredComTodosOsCamposDoEnvelope)
            .RootElement.EnumerateObject()
            .Select(propriedade => propriedade.Name)
            .ToHashSet();

        Assert.Equal(camposDoEnvelope, ClassificacaoDosCamposDoEnvelopeTradeRegistered.Keys.ToHashSet());
    }

    [Fact]
    public void CamposDoEnvelopeTradeRegistered_ColunaOuCampoQueCarregaAdiante_ChegaIntactoNoEventoParseado()
    {
        var resultado = TradeRegisteredPayload.Deserializar(PayloadTradeRegisteredComTodosOsCamposDoEnvelope);

        Assert.True(resultado.IsSuccess);
        var evento = resultado.Value;

        Assert.Equal("op-7f3a", evento.TradeId);
        Assert.Equal("cli-001", evento.ClienteId);
        Assert.Equal(OperacaoTrade.Aplicacao, evento.Operacao);
        Assert.Equal(new DateOnly(2026, 8, 1), evento.DataEvento);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 14, 2, 11, TimeSpan.Zero), evento.RegistradoEm);
        Assert.Equal("op-3c9b", evento.EstornaTradeId);
    }

    [Fact]
    public void CamposDoEnvelopeTradeRegistered_VDivergente_EhValidadoEViraVersaoNaoSuportada()
    {
        var payload = PayloadTradeRegisteredComTodosOsCamposDoEnvelope.Replace("\"v\": 1,", "\"v\": 2,");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.VersaoNaoSuportada, resultado.Error);
    }

    [Fact]
    public void CamposDoEnvelopeTradeRegistered_TipoDivergente_EhValidadoEViraPayloadInvalido()
    {
        var payload = PayloadTradeRegisteredComTodosOsCamposDoEnvelope.Replace(
            "\"tipo\": \"TradeRegistered\",", "\"tipo\": \"PriceObserved\",");

        var resultado = TradeRegisteredPayload.Deserializar(payload);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TradeRegisteredErrors.PayloadInvalido, resultado.Error);
    }

    [Fact]
    public async Task CamposDoEnvelopeTradeRegistered_ValorOrigemSaldoDeterminaExistenciaEQtdDeltaDaSegundaLinha()
    {
        var (handlerSemOrigem, movimentoWriteSemOrigem, _, _, _, _) = CriarHandler();
        var eventoSemOrigem = CriarEvento(tradeId: "op-sem-origem", valorFinanceiro: 1000m, valorOrigemSaldoBruto: "0");
        await handlerSemOrigem.Handle(new ProcessarTradeRegisteredCommand(eventoSemOrigem), CancellationToken.None);
        Assert.Single(movimentoWriteSemOrigem.Adicionados);

        var (handlerComOrigem, movimentoWriteComOrigem, _, _, _, _) = CriarHandler();
        var eventoComOrigem = CriarEvento(tradeId: "op-com-origem", valorFinanceiro: 1000m, valorOrigemSaldoBruto: "400.00");
        await handlerComOrigem.Handle(new ProcessarTradeRegisteredCommand(eventoComOrigem), CancellationToken.None);

        Assert.Equal(2, movimentoWriteComOrigem.Adicionados.Count);
        var perna = movimentoWriteComOrigem.Adicionados.Single(m => m.InstrumentoId == InstrumentosCaixa.Brl);
        Assert.Equal(-400.00m, perna.QtdDelta);
    }

    [Theory]
    [InlineData("td:outro-instrumento", 10, 1000)]
    [InlineData("td:tesouro-ipca-2035-05-15", 5, 1000)]
    [InlineData("td:tesouro-ipca-2035-05-15", 10, 500)]
    public async Task CamposDoEnvelopeTradeRegistered_InstrumentoIdQuantidadeValorFinanceiroNoEstorno_SaoConferidosContraOTitulo(
        string instrumentoIdDoEstorno, decimal quantidadeDoEstorno, decimal valorFinanceiroDoEstorno)
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
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
        Assert.Equal(MotivoParking.EstornoDivergente, resultado.Value.Motivo);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task CamposDoEnvelopeTradeRegistered_InstrumentoIdQuantidadeValorFinanceiroNoEstorno_QuandoConferidosNaoSaoGravados()
    {
        var titulo = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-7f3a");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [titulo]);
        var evento = CriarEvento(
            tradeId: "op-estorno-1",
            operacao: OperacaoTrade.Estorno,
            instrumentoId: InstrumentoId,
            quantidade: 10m,
            valorFinanceiro: 1000m,
            estornaTradeId: "op-7f3a");

        await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        var ajuste = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(titulo.InstrumentoId, ajuste.InstrumentoId);
        Assert.Equal(-titulo.QtdDelta, ajuste.QtdDelta);
        Assert.Equal(-titulo.ValorFinanceiro, ajuste.ValorFinanceiro);
        Assert.NotEqual(evento.Quantidade, ajuste.QtdDelta);
        Assert.NotEqual(evento.ValorFinanceiro, ajuste.ValorFinanceiro);
    }

    [Fact]
    public async Task Handle_ResgateComBasePositivaEPrazoMenorQueTrintaDias_GravaAsQuatroLinhasComRefExternaPropria()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-compra-1");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [compra]);
        var evento = CriarEvento(
            tradeId: "op-resgate-1", operacao: OperacaoTrade.Resgate, quantidade: 10m, valorFinanceiro: 1200m, diaEvento: 10);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(4, movimentoWrite.Adicionados.Count);

        var venda = movimentoWrite.Adicionados.Single(m => m.RefExterna == "op-resgate-1");
        Assert.Equal(TipoMovimento.Venda, venda.Tipo);
        Assert.Equal(InstrumentoId, venda.InstrumentoId);
        Assert.Equal(-10m, venda.QtdDelta);
        Assert.Equal(1200m, venda.ValorFinanceiro);

        var ir = movimentoWrite.Adicionados.Single(m => m.RefExterna == "ir:op-resgate-1");
        Assert.Equal(TipoMovimento.IrRetido, ir.Tipo);
        Assert.Equal(InstrumentosCaixa.ALiquidar, ir.InstrumentoId);
        Assert.Equal(-15.30m, ir.QtdDelta);
        Assert.Equal(15.30m, ir.ValorFinanceiro);

        var iof = movimentoWrite.Adicionados.Single(m => m.RefExterna == "iof:op-resgate-1");
        Assert.Equal(TipoMovimento.Iof, iof.Tipo);
        Assert.Equal(InstrumentosCaixa.ALiquidar, iof.InstrumentoId);
        Assert.Equal(-132.00m, iof.QtdDelta);
        Assert.Equal(132.00m, iof.ValorFinanceiro);

        var aliq = movimentoWrite.Adicionados.Single(m => m.RefExterna == "aliq:op-resgate-1");
        Assert.Equal(TipoMovimento.ALiquidar, aliq.Tipo);
        Assert.Equal(InstrumentosCaixa.ALiquidar, aliq.InstrumentoId);
        Assert.Equal(1200m, aliq.QtdDelta);
        Assert.Equal(1200m, aliq.ValorFinanceiro);
    }

    [Fact]
    public async Task Handle_ResgateComTributos_SomaDosQtdDeltaEmCaixaALiquidarEhOValorBrutoMenosIrMenosIof()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-compra-1");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [compra]);
        var evento = CriarEvento(
            tradeId: "op-resgate-1", operacao: OperacaoTrade.Resgate, quantidade: 10m, valorFinanceiro: 1200m, diaEvento: 10);

        await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        var venda = movimentoWrite.Adicionados.Single(m => m.RefExterna == "op-resgate-1");
        Assert.Equal(1200m, venda.ValorFinanceiro);

        var somaCaixaALiquidar = movimentoWrite.Adicionados
            .Where(m => m.InstrumentoId == InstrumentosCaixa.ALiquidar)
            .Sum(m => m.QtdDelta);

        Assert.Equal(1052.70m, somaCaixaALiquidar);
        Assert.NotEqual(1200m, somaCaixaALiquidar);
    }

    [Fact]
    public async Task Handle_ResgateSemLoteComPrazoMenorQueTrintaDias_GravaTresLinhasSemLinhaDeIof()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-compra-1");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [compra]);
        var evento = CriarEvento(
            tradeId: "op-resgate-1", operacao: OperacaoTrade.Resgate, quantidade: 10m, valorFinanceiro: 1200m, diaEvento: 40);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(3, movimentoWrite.Adicionados.Count);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == "iof:op-resgate-1");

        var ir = movimentoWrite.Adicionados.Single(m => m.RefExterna == "ir:op-resgate-1");
        Assert.Equal(-45.00m, ir.QtdDelta);
    }

    [Fact]
    public async Task Handle_ResgateComSomaDasBasesPositivasIgualAZero_GravaDuasLinhasSemIrNemIof()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 3000m, "op-compra-b");

        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: [compra]);
        var evento = CriarEvento(
            tradeId: "op-resgate-prejuizo", operacao: OperacaoTrade.Resgate, quantidade: 10m, valorFinanceiro: 1000m, diaEvento: 5);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, movimentoWrite.Adicionados.Count);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == "ir:op-resgate-prejuizo");
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == "iof:op-resgate-prejuizo");
    }

    [Fact]
    public async Task Handle_ResgateComFilaQueNaoCobreAVenda_GravaOConjuntoCompletoEDisparaOSinalDePrecoMedioProvisorio()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 500m, "op-compra-1");

        var (handler, movimentoWrite, _, _, metrics, _) = CriarHandler(movimentosExistentes: [compra]);
        var evento = CriarEvento(
            tradeId: "op-resgate-descoberto", operacao: OperacaoTrade.Resgate, quantidade: 15m, valorFinanceiro: 2000m, diaEvento: 40);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);

        var ir = movimentoWrite.Adicionados.SingleOrDefault(m => m.RefExterna == "ir:op-resgate-descoberto");
        Assert.NotNull(ir);
        Assert.True(ir!.QtdDelta < 0m);

        var sinalizacao = Assert.Single(metrics.SinalizacoesDeResgateSobrePrecoMedioProvisorio);
        Assert.Equal(ClienteId, sinalizacao.ClienteId);
        Assert.Equal(InstrumentoId, sinalizacao.InstrumentoId);
        Assert.Equal(5m, sinalizacao.QuantidadeDescoberta);
    }

    [Fact]
    public async Task Handle_ResgateComFilaQueCobreAVendaComLotesDeDatasDiferentes_NaoDisparaOSinalDePrecoMedioProvisorio()
    {
        var compra1 = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 5m, 250m, "op-compra-1");
        var compra2 = MovimentoTestFactory.Criar(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(10), Instante(1), 10m, 600m, "op-compra-2");

        var (handler, _, _, _, metrics, _) = CriarHandler(movimentosExistentes: [compra1, compra2]);
        var evento = CriarEvento(
            tradeId: "op-resgate-coberto", operacao: OperacaoTrade.Resgate, quantidade: 12m, valorFinanceiro: 1500m, diaEvento: 40);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(metrics.SinalizacoesDeResgateSobrePrecoMedioProvisorio);
    }

    [Fact]
    public async Task Handle_ResgateRetroativoComCompraJaGravadaDeDataEventoPosterior_NaoConsomeAqueleLote_TributaPelosLotesQueExistiamENaoLanca()
    {
        var compraAntiga = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 5m, 500m, "op-compra-antiga");
        var compraFutura = MovimentoTestFactory.Criar(
            2, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(20), Instante(1), 10m, 1200m, "op-compra-futura");

        var (handler, movimentoWrite, _, _, metrics, _) = CriarHandler(
            movimentosExistentes: [compraAntiga, compraFutura]);

        var evento = CriarEvento(
            tradeId: "op-resgate-retroativo",
            operacao: OperacaoTrade.Resgate,
            quantidade: 10m,
            valorFinanceiro: 500m,
            diaEvento: 10);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);

        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == "ir:op-resgate-retroativo");
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == "iof:op-resgate-retroativo");

        var sinalizacaoDeFilaInsuficiente = Assert.Single(metrics.SinalizacoesDeResgateSobrePrecoMedioProvisorio);
        Assert.Equal(ClienteId, sinalizacaoDeFilaInsuficiente.ClienteId);
        Assert.Equal(InstrumentoId, sinalizacaoDeFilaInsuficiente.InstrumentoId);
        Assert.Equal(5m, sinalizacaoDeFilaInsuficiente.QuantidadeDescoberta);
    }

    [Fact]
    public async Task Handle_ReentregaDeResgateJaTributado_NaoDuplicaNenhumDosQuatroMovimentosDerivados()
    {
        var compra = MovimentoTestFactory.Criar(
            1, ClienteId, InstrumentoId, TipoMovimento.Compra, Dia(0), Instante(0), 10m, 1000m, "op-compra-1");

        var (handlerPrimeiraVez, movimentoWritePrimeiraVez, _, _, _, _) = CriarHandler(movimentosExistentes: [compra]);
        var evento = CriarEvento(
            tradeId: "op-resgate-1", operacao: OperacaoTrade.Resgate, quantidade: 10m, valorFinanceiro: 1200m, diaEvento: 10);

        await handlerPrimeiraVez.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);
        Assert.Equal(4, movimentoWritePrimeiraVez.Adicionados.Count);

        var movimentosPersistidos = new List<Movimento> { compra };
        var idSeguinte = 2L;
        foreach (var gravado in movimentoWritePrimeiraVez.Adicionados)
        {
            movimentosPersistidos.Add(MovimentoTestFactory.Criar(
                idSeguinte++,
                gravado.ClienteId,
                gravado.InstrumentoId,
                gravado.Tipo,
                gravado.DataEvento,
                gravado.RegistradoEm,
                gravado.QtdDelta,
                gravado.ValorFinanceiro,
                gravado.RefExterna,
                gravado.RefEstorno));
        }

        var (handlerSegundaVez, movimentoWriteSegundaVez, _, unitOfWorkSegundaVez, _, _) =
            CriarHandler(movimentosExistentes: movimentosPersistidos);

        var resultado = await handlerSegundaVez.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.True(resultado.Value.Replay);
        Assert.Empty(movimentoWriteSegundaVez.Adicionados);
        Assert.Equal(0, unitOfWorkSegundaVez.ChamadasDeSaveChanges);
    }

    private sealed record FatoDeResgate(
        Movimento Venda, Movimento? Ir, Movimento? Iof, Movimento Aliq, Movimento? LiqAliq, Movimento? LiqBrl)
    {
        public IReadOnlyList<Movimento> Linhas() =>
            new[] { Venda, Ir, Iof, Aliq, LiqAliq, LiqBrl }.Where(m => m is not null).Select(m => m!).ToList();
    }

    private static FatoDeResgate CriarFatoDeResgate(
        string clienteId,
        string instrumentoId,
        string tradeId,
        decimal quantidade,
        decimal valorBruto,
        decimal ir = 0m,
        decimal iof = 0m,
        bool liquidado = false)
    {
        var dataEvento = Dia(0);
        var registradoEm = Instante(0);
        var id = 100L;

        var venda = MovimentoTestFactory.Criar(
            id++, clienteId, instrumentoId, TipoMovimento.Venda, dataEvento, registradoEm, -quantidade, valorBruto, tradeId);

        var irLinha = ir > 0m
            ? MovimentoTestFactory.Criar(
                id++, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido, dataEvento, registradoEm, -ir, ir, $"ir:{tradeId}")
            : null;

        var iofLinha = iof > 0m
            ? MovimentoTestFactory.Criar(
                id++, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Iof, dataEvento, registradoEm, -iof, iof, $"iof:{tradeId}")
            : null;

        var aliq = MovimentoTestFactory.Criar(
            id++, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, dataEvento, registradoEm, valorBruto, valorBruto, $"aliq:{tradeId}");

        Movimento? liqAliq = null;
        Movimento? liqBrl = null;

        if (liquidado)
        {
            var saldo = valorBruto - ir - iof;
            var dataLiquidacao = dataEvento.AddDays(1);

            liqAliq = MovimentoTestFactory.Criar(
                id++, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao, dataLiquidacao, registradoEm, -saldo, saldo, $"liq:{tradeId}:aliq");

            liqBrl = MovimentoTestFactory.Criar(
                id++, clienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao, dataLiquidacao, registradoEm, saldo, saldo, $"liq:{tradeId}:brl");
        }

        return new FatoDeResgate(venda, irLinha, iofLinha, aliq, liqAliq, liqBrl);
    }

    private TradeRegisteredEvento CriarEventoDeEstornoDoFato(FatoDeResgate fato, string tradeIdDoEstorno = "op-estorno-resgate") =>
        CriarEvento(
            tradeId: tradeIdDoEstorno,
            operacao: OperacaoTrade.Estorno,
            instrumentoId: fato.Venda.InstrumentoId,
            quantidade: Math.Abs(fato.Venda.QtdDelta),
            valorFinanceiro: fato.Venda.ValorFinanceiro,
            diaEvento: 5,
            estornaTradeId: fato.Venda.RefExterna);

    [Fact]
    public async Task Handle_EstornoDeResgateAntesDaLiquidacao_ReverteVendaIrIofEALiquidar_ENaoTentaReverterLiquidacaoInexistente()
    {
        var fato = CriarFatoDeResgate(ClienteId, InstrumentoId, "op-resgate-1", quantidade: 10m, valorBruto: 1200m, ir: 15.30m, iof: 132.00m);
        var (handler, movimentoWrite, posicaoWrite, _, _, travamento) = CriarHandler(movimentosExistentes: fato.Linhas());
        var evento = CriarEventoDeEstornoDoFato(fato);

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.Contains((ClienteId, $"aliq:{fato.Venda.RefExterna}"), travamento.Travamentos);

        Assert.Equal(4, movimentoWrite.Adicionados.Count);
        Assert.All(movimentoWrite.Adicionados, m => Assert.Equal(TipoMovimento.Ajuste, m.Tipo));

        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == evento.TradeId && m.RefEstorno == fato.Venda.Id);
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:aliq:{evento.TradeId}" && m.RefEstorno == fato.Aliq.Id);
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:ir:{evento.TradeId}" && m.RefEstorno == fato.Ir!.Id);
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:iof:{evento.TradeId}" && m.RefEstorno == fato.Iof!.Id);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna.StartsWith("est:liq:", StringComparison.Ordinal));

        Assert.Equal(new PosicaoTresColunas(0m, 0m, 0m), posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentosCaixa.ALiquidar));
    }

    [Fact]
    public async Task Handle_EstornoDeResgateDepoisDaLiquidacao_ReverteAsSeisLinhas_ETrasPosicoesDeCaixaAZero()
    {
        var fato = CriarFatoDeResgate(
            ClienteId, InstrumentoId, "op-resgate-2", quantidade: 10m, valorBruto: 1200m, ir: 15.30m, iof: 132.00m, liquidado: true);
        var (handler, movimentoWrite, posicaoWrite, _, _, _) = CriarHandler(movimentosExistentes: fato.Linhas());
        var evento = CriarEventoDeEstornoDoFato(fato, "op-estorno-resgate-2");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.Equal(6, movimentoWrite.Adicionados.Count);
        Assert.All(movimentoWrite.Adicionados, m => Assert.Equal(TipoMovimento.Ajuste, m.Tipo));

        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:liq:{evento.TradeId}:aliq" && m.RefEstorno == fato.LiqAliq!.Id);
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:liq:{evento.TradeId}:brl" && m.RefEstorno == fato.LiqBrl!.Id);

        Assert.Equal(new PosicaoTresColunas(0m, 0m, 0m), posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentosCaixa.ALiquidar));
        Assert.Equal(new PosicaoTresColunas(0m, 0m, 0m), posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentosCaixa.Brl));
    }

    [Fact]
    public async Task Handle_EstornoDeResgateSemIof_NaoTentaReverterIofInexistente()
    {
        var fato = CriarFatoDeResgate(
            ClienteId, InstrumentoId, "op-resgate-3", quantidade: 10m, valorBruto: 1200m, ir: 45.00m, iof: 0m, liquidado: true);
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: fato.Linhas());
        var evento = CriarEventoDeEstornoDoFato(fato, "op-estorno-resgate-3");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(5, movimentoWrite.Adicionados.Count);
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:ir:{evento.TradeId}");
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == $"est:iof:{evento.TradeId}");
    }

    [Fact]
    public async Task Handle_EstornoDeResgateSemIrNemIof_ReverteApenasVendaEALiquidar()
    {
        var fato = CriarFatoDeResgate(
            ClienteId, InstrumentoId, "op-resgate-4", quantidade: 10m, valorBruto: 1000m, ir: 0m, iof: 0m, liquidado: true);
        var (handler, movimentoWrite, _, _, _, _) = CriarHandler(movimentosExistentes: fato.Linhas());
        var evento = CriarEventoDeEstornoDoFato(fato, "op-estorno-resgate-4");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(4, movimentoWrite.Adicionados.Count);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == $"est:ir:{evento.TradeId}");
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna == $"est:iof:{evento.TradeId}");
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:aliq:{evento.TradeId}");
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:liq:{evento.TradeId}:aliq");
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == $"est:liq:{evento.TradeId}:brl");
    }

    [Fact]
    public async Task Handle_EstornoDeResgateCujoALiquidarAindaNaoExiste_AntesDaConciliacaoExistir_ReverteSoOPrincipal_NaoTentaReverterDerivados()
    {
        var fato = CriarFatoDeResgate(ClienteId, InstrumentoId, "op-resgate-legado", quantidade: 10m, valorBruto: 1200m);
        var (handler, movimentoWrite, _, _, _, travamento) = CriarHandler(movimentosExistentes: [fato.Venda]);
        var evento = CriarEventoDeEstornoDoFato(fato, "op-estorno-legado");

        var resultado = await handler.Handle(new ProcessarTradeRegisteredCommand(evento), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoTradeRegisteredTipo.Escriturado, resultado.Value.Tipo);
        Assert.Contains((ClienteId, $"aliq:{fato.Venda.RefExterna}"), travamento.Travamentos);

        var ajuste = Assert.Single(movimentoWrite.Adicionados);
        Assert.Equal(fato.Venda.Id, ajuste.RefEstorno);
        Assert.Equal(evento.TradeId, ajuste.RefExterna);
    }
}

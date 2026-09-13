using Custodia.Application.Calendario;
using Custodia.Application.Liquidacao;
using Custodia.Application.Posicoes;
using Custodia.Application.Tests.Fakes;
using Custodia.Domain.Calendario;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Microsoft.Extensions.Configuration;

namespace Custodia.Application.Tests.Liquidacao;

public sealed class LiquidarResgatesVencidosCommandHandlerTests
{
    private const string ClienteId = "cli-001";
    private const string InstrumentoId = "td:tesouro-ipca-2035-05-15";

    private static readonly DateOnly DataDoResgate = new(2026, 8, 10);
    private static readonly DateOnly DataDeLiquidacaoPadrao = new(2026, 8, 11);
    private static readonly DateTimeOffset RegistradoEmDoResgate = new(2026, 8, 10, 14, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private static IConfiguration CriarConfiguracao(long? teto = null)
    {
        var dados = new Dictionary<string, string?>();
        if (teto is not null)
        {
            dados["Decisao:LiquidacaoTetoPorCiclo"] = teto.Value.ToString();
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
    }

    private sealed record FatoParaJob(Movimento Venda, Movimento? Ir, Movimento? Iof, Movimento Aliq)
    {
        public IReadOnlyList<Movimento> Linhas() =>
            new[] { Venda, Ir, Iof, Aliq }.Where(m => m is not null).Select(m => m!).ToList();
    }

    private static FatoParaJob CriarFato(
        string tradeId, decimal quantidade, decimal valorBruto, decimal ir = 0m, decimal iof = 0m, long idInicial = 1)
    {
        var id = idInicial;

        var venda = MovimentoTestFactory.Criar(
            id++, ClienteId, InstrumentoId, TipoMovimento.Venda, DataDoResgate, RegistradoEmDoResgate, -quantidade, valorBruto, tradeId);

        var irLinha = ir > 0m
            ? MovimentoTestFactory.Criar(
                id++, ClienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido, DataDoResgate, RegistradoEmDoResgate, -ir, ir, $"ir:{tradeId}")
            : null;

        var iofLinha = iof > 0m
            ? MovimentoTestFactory.Criar(
                id++, ClienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Iof, DataDoResgate, RegistradoEmDoResgate, -iof, iof, $"iof:{tradeId}")
            : null;

        var aliq = MovimentoTestFactory.Criar(
            id, ClienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar, DataDoResgate, RegistradoEmDoResgate, valorBruto, valorBruto, $"aliq:{tradeId}");

        return new FatoParaJob(venda, irLinha, iofLinha, aliq);
    }

    private static (
        LiquidarResgatesVencidosCommandHandler Handler,
        FakeMovimentoWriteRepository MovimentoWrite,
        FakePosicaoCorrenteWriteRepository PosicaoWrite,
        FakeUnitOfWork UnitOfWork,
        FakeBusinessMetrics Metrics,
        FakeMovimentoTravamentoRepository Travamento,
        FakeRecalculoEnfileiradorPort Recalculo) CriarHandler(
        IReadOnlyList<AReceberVencido> candidatas,
        IReadOnlyList<Movimento> movimentosExistentes,
        DateOnly hoje,
        Func<DateOnly, Result<ProximoDiaUtilConsulta>>? proximoDiaUtil = null,
        long? teto = null,
        DateTimeOffset? agora = null)
    {
        var candidataRead = new FakeAReceberVencidoReadRepository(candidatas);
        var movimentoRead = new FakeMovimentoReadRepository(movimentosExistentes);
        var movimentoWrite = new FakeMovimentoWriteRepository();
        var travamento = new FakeMovimentoTravamentoRepository(movimentosExistentes);
        var posicaoRead = new FakePosicaoCorrenteReadRepository();
        var posicaoWrite = new FakePosicaoCorrenteWriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var calendarioRead = new FakeCalendarioDiasUteisReadRepository(
            proximoDiaUtil: proximoDiaUtil ?? (data => Result<ProximoDiaUtilConsulta>.Success(ProximoDiaUtilConsulta.De(data.AddDays(1)))),
            horizonte: () => Result<HorizonteCalendarioConsulta>.Success(new HorizonteCalendarioConsulta(new DateOnly(2030, 12, 31), hoje)));
        var proximoDiaUtilService = new ProximoDiaUtilService(calendarioRead);
        var aplicadorIncrementalDePosicao = new AplicadorIncrementalDePosicao(movimentoRead, posicaoRead);
        var recalculo = new FakeRecalculoEnfileiradorPort();
        var metrics = new FakeBusinessMetrics();
        var configuration = CriarConfiguracao(teto);
        var timeProvider = new FixedTimeProvider(agora ?? new DateTimeOffset(hoje.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

        var handler = new LiquidarResgatesVencidosCommandHandler(
            candidataRead,
            travamento,
            movimentoRead,
            movimentoWrite,
            posicaoWrite,
            aplicadorIncrementalDePosicao,
            unitOfWork,
            proximoDiaUtilService,
            calendarioRead,
            recalculo,
            metrics,
            new FakePontoDeSuspensaoAposTravamento(),
            configuration,
            timeProvider);

        return (handler, movimentoWrite, posicaoWrite, unitOfWork, metrics, travamento, recalculo);
    }

    [Fact]
    public async Task Handle_SemCandidatas_DevolveCompletudeComContadoresZerados()
    {
        var (handler, movimentoWrite, _, _, _, _, _) = CriarHandler([], [], DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(DesfechoLiquidacaoDeResgates.Completude, resultado.Value.Desfecho);
        Assert.Equal(0, resultado.Value.CandidatasExaminadas);
        Assert.Equal(0, resultado.Value.FatosLiquidados);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_CandidatasAcimaDoTeto_DevolveFalha_RegistraMetrica_NenhumTravamentoOcorre()
    {
        var fato1 = CriarFato("op-1", 10m, 1200m, idInicial: 1);
        var fato2 = CriarFato("op-2", 10m, 1200m, idInicial: 10);
        var candidatas = new[]
        {
            new AReceberVencido(ClienteId, "op-1", DataDoResgate),
            new AReceberVencido(ClienteId, "op-2", DataDoResgate),
        };

        var (handler, movimentoWrite, _, _, metrics, travamento, _) = CriarHandler(
            candidatas, [.. fato1.Linhas(), .. fato2.Linhas()], DataDeLiquidacaoPadrao, teto: 1);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(LiquidacaoErrors.LimiteDeCandidatasPorCicloExcedido, resultado.Error);
        Assert.Empty(movimentoWrite.Adicionados);
        Assert.Empty(travamento.Travamentos);

        var limite = Assert.Single(metrics.LimitesPorTetoDeLiquidacao);
        Assert.Equal(2, limite.CandidatasEncontradas);
        Assert.Equal(1, limite.Teto);
    }

    [Fact]
    public async Task Handle_CandidataVencida_InsereAsDuasPernasComSaldoLiquido_EAtualizaAsDuasPosicoes()
    {
        var fato = CriarFato("op-resgate-1", 10m, 1200m, ir: 15.30m, iof: 132.00m);
        var candidatas = new[] { new AReceberVencido(ClienteId, "op-resgate-1", DataDoResgate) };

        var (handler, movimentoWrite, posicaoWrite, _, _, travamento, _) = CriarHandler(
            candidatas, fato.Linhas(), hoje: DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.FatosLiquidados);
        Assert.Contains((ClienteId, "aliq:op-resgate-1"), travamento.Travamentos);

        Assert.Equal(2, movimentoWrite.Adicionados.Count);

        var pernaAliq = movimentoWrite.Adicionados.Single(m => m.RefExterna == "liq:op-resgate-1:aliq");
        Assert.Equal(TipoMovimento.Liquidacao, pernaAliq.Tipo);
        Assert.Equal(InstrumentosCaixa.ALiquidar, pernaAliq.InstrumentoId);
        Assert.Equal(DataDeLiquidacaoPadrao, pernaAliq.DataEvento);
        Assert.Equal(-1052.70m, pernaAliq.QtdDelta);
        Assert.Equal(1052.70m, pernaAliq.ValorFinanceiro);

        var pernaBrl = movimentoWrite.Adicionados.Single(m => m.RefExterna == "liq:op-resgate-1:brl");
        Assert.Equal(TipoMovimento.Liquidacao, pernaBrl.Tipo);
        Assert.Equal(InstrumentosCaixa.Brl, pernaBrl.InstrumentoId);
        Assert.Equal(DataDeLiquidacaoPadrao, pernaBrl.DataEvento);
        Assert.Equal(1052.70m, pernaBrl.QtdDelta);
        Assert.Equal(1052.70m, pernaBrl.ValorFinanceiro);

        Assert.NotNull(posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentosCaixa.ALiquidar));
        Assert.NotNull(posicaoWrite.UltimaPosicaoDe(ClienteId, InstrumentosCaixa.Brl));
    }

    [Fact]
    public async Task Handle_SemIrNemIof_SaldoIgualAoValorBrutoDoALiquidar()
    {
        var fato = CriarFato("op-resgate-sem-tributo", 10m, 1000m);
        var candidatas = new[] { new AReceberVencido(ClienteId, "op-resgate-sem-tributo", DataDoResgate) };

        var (handler, movimentoWrite, _, _, _, _, _) = CriarHandler(candidatas, fato.Linhas(), DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var pernaAliq = movimentoWrite.Adicionados.Single(m => m.RefExterna == "liq:op-resgate-sem-tributo:aliq");
        Assert.Equal(-1000m, pernaAliq.QtdDelta);
    }

    [Fact]
    public async Task Handle_CandidataAindaNaoVencida_NaoInsereNada_ContaComoNaoVencida()
    {
        var fato = CriarFato("op-resgate-recente", 10m, 1200m);
        var candidatas = new[] { new AReceberVencido(ClienteId, "op-resgate-recente", DataDoResgate) };

        var (handler, movimentoWrite, _, unitOfWork, _, _, _) = CriarHandler(
            candidatas, fato.Linhas(), hoje: DataDoResgate);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, resultado.Value.FatosLiquidados);
        Assert.Equal(1, resultado.Value.FatosNaoVencidos);
        Assert.Empty(movimentoWrite.Adicionados);
        Assert.Equal(1, unitOfWork.ChamadasDeDescartarTransacao);
    }

    [Fact]
    public async Task Handle_CandidataComAliqJaRevertida_NaoLiquida_ComControlePositivoDeOutraNaoRevertidaNoMesmoCiclo()
    {
        var fatoRevertido = CriarFato("op-revertido", 10m, 1200m, idInicial: 1);
        var ajusteRevertendoAliq = MovimentoTestFactory.Criar(
            50, ClienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Ajuste, DataDoResgate.AddDays(2), RegistradoEmDoResgate,
            -1200m, -1200m, "est:aliq:op-estorno-1", refEstorno: fatoRevertido.Aliq.Id);

        var fatoValido = CriarFato("op-valido", 10m, 1200m, idInicial: 10);

        var candidatas = new[]
        {
            new AReceberVencido(ClienteId, "op-revertido", DataDoResgate),
            new AReceberVencido(ClienteId, "op-valido", DataDoResgate),
        };

        var movimentosExistentes = new List<Movimento>();
        movimentosExistentes.AddRange(fatoRevertido.Linhas());
        movimentosExistentes.Add(ajusteRevertendoAliq);
        movimentosExistentes.AddRange(fatoValido.Linhas());

        var (handler, movimentoWrite, _, _, _, _, _) = CriarHandler(candidatas, movimentosExistentes, DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.FatosLiquidados);
        Assert.Equal(1, resultado.Value.FatosJaTratados);
        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna.StartsWith("liq:op-revertido", StringComparison.Ordinal));
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == "liq:op-valido:aliq");
    }

    [Fact]
    public async Task Handle_CandidataComMovimentoPrincipalRevertido_NaoLiquida()
    {
        var fato = CriarFato("op-principal-revertido", 10m, 1200m, idInicial: 1);
        var ajusteRevertendoPrincipal = MovimentoTestFactory.Criar(
            50, ClienteId, InstrumentoId, TipoMovimento.Ajuste, DataDoResgate.AddDays(2), RegistradoEmDoResgate,
            10m, -1200m, "est:op-estorno-1", refEstorno: fato.Venda.Id);

        var candidatas = new[] { new AReceberVencido(ClienteId, "op-principal-revertido", DataDoResgate) };
        var movimentosExistentes = new List<Movimento>(fato.Linhas()) { ajusteRevertendoPrincipal };

        var (handler, movimentoWrite, _, _, _, _, _) = CriarHandler(candidatas, movimentosExistentes, DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, resultado.Value.FatosLiquidados);
        Assert.Equal(1, resultado.Value.FatosJaTratados);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_CandidataOrfaSemMovimentoPrincipal_EPulada_EAlertaComARefExternaOfensora_ComControlePositivoDeOutroFatoDoMesmoCiclo()
    {
        var fatoOrfao = CriarFato("op-orfao", 10m, 1200m, idInicial: 1);
        var fatoValido = CriarFato("op-valido-no-mesmo-ciclo", 10m, 1200m, idInicial: 10);

        var candidatas = new[]
        {
            new AReceberVencido(ClienteId, "op-orfao", DataDoResgate),
            new AReceberVencido(ClienteId, "op-valido-no-mesmo-ciclo", DataDoResgate),
        };

        var movimentosExistentes = new List<Movimento> { fatoOrfao.Aliq };
        movimentosExistentes.AddRange(fatoValido.Linhas());

        var (handler, movimentoWrite, _, unitOfWork, metrics, _, _) = CriarHandler(
            candidatas, movimentosExistentes, DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(DesfechoLiquidacaoDeResgates.DadoQuebrado, resultado.Value.Desfecho);
        Assert.Equal(1, resultado.Value.FatosInconsistentes);
        Assert.Equal(1, resultado.Value.FatosLiquidados);

        Assert.DoesNotContain(movimentoWrite.Adicionados, m => m.RefExterna.StartsWith("liq:op-orfao", StringComparison.Ordinal));
        Assert.Contains(movimentoWrite.Adicionados, m => m.RefExterna == "liq:op-valido-no-mesmo-ciclo:aliq");

        var alerta = Assert.Single(metrics.CandidatasInconsistentesNaLiquidacao);
        Assert.Equal(ClienteId, alerta.ClienteId);
        Assert.Equal("op-orfao", alerta.TradeId);
        Assert.Equal("aliq:op-orfao", alerta.RefExternaOfensora);

        Assert.Equal(1, unitOfWork.ChamadasDeDescartarTransacao);
    }

    [Fact]
    public async Task Handle_CandidataComLiqAliqJaExistente_NaoDuplica()
    {
        var fato = CriarFato("op-ja-liquidado", 10m, 1200m, idInicial: 1);
        var liqAliqExistente = MovimentoTestFactory.Criar(
            50, ClienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao, DataDeLiquidacaoPadrao, RegistradoEmDoResgate,
            -1200m, 1200m, "liq:op-ja-liquidado:aliq");

        var candidatas = new[] { new AReceberVencido(ClienteId, "op-ja-liquidado", DataDoResgate) };
        var movimentosExistentes = new List<Movimento>(fato.Linhas()) { liqAliqExistente };

        var (handler, movimentoWrite, _, _, _, _, _) = CriarHandler(candidatas, movimentosExistentes, DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.FatosJaTratados);
        Assert.Empty(movimentoWrite.Adicionados);
    }

    [Fact]
    public async Task Handle_CalendarioExaurido_DevolveFalha_RegistraMetricaDeAlerta()
    {
        var fato = CriarFato("op-calendario-exaurido", 10m, 1200m);
        var candidatas = new[] { new AReceberVencido(ClienteId, "op-calendario-exaurido", DataDoResgate) };

        var (handler, movimentoWrite, _, unitOfWork, metrics, _, _) = CriarHandler(
            candidatas,
            fato.Linhas(),
            hoje: DataDeLiquidacaoPadrao,
            proximoDiaUtil: _ => CalendarioDiasUteisErrors.HorizonteEsgotado);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(CalendarioDiasUteisErrors.HorizonteEsgotado, resultado.Error);
        Assert.Empty(movimentoWrite.Adicionados);
        Assert.Equal(1, unitOfWork.ChamadasDeDescartarTransacao);

        var alerta = Assert.Single(metrics.CalendariosExauridosNaLiquidacao);
        Assert.Equal(ClienteId, alerta.ClienteId);
        Assert.Equal("op-calendario-exaurido", alerta.TradeId);
    }

    [Fact]
    public async Task Handle_CatchUpRetroativo_GravaNaDataDeLiquidacaoPassada_EEnfileiraRecalculoParaOsDoisInstrumentos()
    {
        var dataDeLiquidacaoRetroativa = DataDoResgate.AddDays(1);
        var fato = CriarFato("op-catchup", 10m, 1200m);
        var candidatas = new[] { new AReceberVencido(ClienteId, "op-catchup", DataDoResgate) };
        var hojeMuitoDepois = dataDeLiquidacaoRetroativa.AddDays(3);

        var (handler, movimentoWrite, _, _, _, _, recalculo) = CriarHandler(
            candidatas, fato.Linhas(), hoje: hojeMuitoDepois);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.FatosLiquidados);

        var pernaAliq = movimentoWrite.Adicionados.Single(m => m.RefExterna == "liq:op-catchup:aliq");
        Assert.Equal(dataDeLiquidacaoRetroativa, pernaAliq.DataEvento);

        Assert.Equal(2, recalculo.Chamadas.Count);
        Assert.Contains(recalculo.Chamadas, c => c.InstrumentoId == InstrumentosCaixa.ALiquidar && c.Desde == dataDeLiquidacaoRetroativa);
        Assert.Contains(recalculo.Chamadas, c => c.InstrumentoId == InstrumentosCaixa.Brl && c.Desde == dataDeLiquidacaoRetroativa);
    }

    [Fact]
    public async Task Handle_DataDeLiquidacaoIgualAHoje_NaoEnfileiraRecalculo()
    {
        var fato = CriarFato("op-em-dia", 10m, 1200m);
        var candidatas = new[] { new AReceberVencido(ClienteId, "op-em-dia", DataDoResgate) };

        var (handler, _, _, _, _, _, recalculo) = CriarHandler(candidatas, fato.Linhas(), hoje: DataDeLiquidacaoPadrao);

        var resultado = await handler.Handle(new LiquidarResgatesVencidosCommand(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(recalculo.Chamadas);
    }
}

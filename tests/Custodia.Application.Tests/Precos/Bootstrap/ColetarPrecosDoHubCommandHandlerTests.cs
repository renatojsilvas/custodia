using Custodia.Application.Calendario;
using Custodia.Application.Precos;
using Custodia.Application.Precos.Bootstrap;
using Custodia.Application.Precos.Hub;
using Custodia.Application.Tests.Common;
using Custodia.Application.Tests.Fakes;
using Custodia.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace Custodia.Application.Tests.Precos.Bootstrap;

public sealed class ColetarPrecosDoHubCommandHandlerTests
{
    private static readonly DateOnly Hoje = new(2026, 9, 10);

    private sealed record Ambiente(
        ColetarPrecosDoHubCommandHandler Handler,
        FakeHubPrecosClient HubClient,
        FakePrecoWriteRepository PrecoWrite,
        FakeUnitOfWork UnitOfWork,
        FakeBusinessMetrics Metrics,
        FakeLogger<ColetarPrecosDoHubCommandHandler> Logger);

    private static Ambiente CriarAmbiente(
        Func<Result<EscopoLivroInteiroConsulta>>? livroInteiro = null,
        Func<Result<IReadOnlyList<string>>>? semPrecoAtual = null,
        Func<Result<HorizonteCalendarioConsulta>>? horizonte = null,
        Func<DateOnly, IReadOnlyList<string>, Result<IReadOnlyList<PrecoAsOfItem>>>? obterFatia = null,
        Func<ObservacaoDePreco, Result<ResultadoHistorico>>? registrarHistorico = null,
        Func<ObservacaoDePreco, Result<ResultadoRegistroBootstrap>>? registrarBootstrap = null,
        int? tetoDias = null,
        int? tamanhoFatia = null,
        int? tetoFatias = null)
    {
        var escopo = new FakeEscopoDePrecosReadRepository(livroInteiro, semPrecoAtual);
        var calendario = new FakeCalendarioDiasUteisReadRepository(horizonte: horizonte
            ?? (() => Result<HorizonteCalendarioConsulta>.Success(new HorizonteCalendarioConsulta(null, Hoje))));
        var hubClient = new FakeHubPrecosClient(obterFatia);
        var precoWrite = new FakePrecoWriteRepository(registrarHistorico, registrarBootstrap: registrarBootstrap);
        var unitOfWork = new FakeUnitOfWork();
        var metrics = new FakeBusinessMetrics();
        var logger = new FakeLogger<ColetarPrecosDoHubCommandHandler>();

        var dados = new Dictionary<string, string?>();
        if (tetoDias is not null)
        {
            dados["Decisao:BootstrapPrecosTetoDias"] = tetoDias.Value.ToString();
        }

        if (tamanhoFatia is not null)
        {
            dados["Decisao:BootstrapPrecosTamanhoFatia"] = tamanhoFatia.Value.ToString();
        }

        if (tetoFatias is not null)
        {
            dados["Decisao:BootstrapPrecosTetoFatias"] = tetoFatias.Value.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dados).Build();

        var handler = new ColetarPrecosDoHubCommandHandler(
            escopo, calendario, hubClient, precoWrite, unitOfWork, metrics, configuration, logger);

        return new Ambiente(handler, hubClient, precoWrite, unitOfWork, metrics, logger);
    }

    private static PrecoAsOfCampo Campo(decimal valor, DateOnly dataRef, string fonte = "td", int revisao = 0) =>
        new(valor, fonte, revisao, new DateTimeOffset(dataRef.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), dataRef);

    [Fact]
    public async Task Handle_LacoADeDatasCompleto_ProcessaTodasAsDatasEDevolveSucesso()
    {
        var desde = Hoje.AddDays(-2);
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:a"], desde)));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(3, resultado.Value.Dias);
        Assert.Equal(3, resultado.Value.ChamadasHttp);
        Assert.Equal(3, ambiente.HubClient.Chamadas.Count);
        Assert.Equal([desde, desde.AddDays(1), desde.AddDays(2)], ambiente.HubClient.Chamadas.Select(c => c.Data));
    }

    [Fact]
    public async Task Handle_JanelaMaiorQueOTetoDeDias_FalhaAntesDeQualquerChamada()
    {
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:a"], Hoje.AddDays(-10))),
            tetoDias: 5);

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal("Hub.ColetaIncompleta", resultado.Error.Code);
        Assert.Empty(ambiente.HubClient.Chamadas);
    }

    [Fact]
    public async Task Handle_ErroNaSegundaData_ParaEATerceiraDataNaoEhPedida()
    {
        var desde = Hoje.AddDays(-2);
        var meio = desde.AddDays(1);

        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:a"], desde)),
            obterFatia: (data, _) => data == meio
                ? Result<IReadOnlyList<PrecoAsOfItem>>.Failure(HubPrecosErrors.HubIndisponivel)
                : Result<IReadOnlyList<PrecoAsOfItem>>.Success(Array.Empty<PrecoAsOfItem>()));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubIndisponivel, resultado.Error);
        Assert.Equal(2, ambiente.HubClient.Chamadas.Count);
        Assert.Equal([desde, meio], ambiente.HubClient.Chamadas.Select(c => c.Data));
    }

    [Fact]
    public async Task Handle_LacoBComDuasVezesATamanhoDaFatiaMaisUm_FazTresChamadas()
    {
        var instrumentos = Enumerable.Range(0, 5).Select(i => $"td:instrumento-{i}").ToList();

        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(instrumentos, Hoje)),
            tamanhoFatia: 2);

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(3, ambiente.HubClient.Chamadas.Count);
        Assert.Equal(1, resultado.Value.Dias);
    }

    [Fact]
    public async Task Handle_NumeroDeFatiasMaiorQueOTeto_FalhaAntesDeQualquerChamada()
    {
        var instrumentos = Enumerable.Range(0, 5).Select(i => $"td:instrumento-{i}").ToList();

        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(instrumentos, Hoje)),
            tamanhoFatia: 1,
            tetoFatias: 2);

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal("Hub.ColetaIncompleta", resultado.Error.Code);
        Assert.Empty(ambiente.HubClient.Chamadas);
    }

    [Fact]
    public async Task Handle_EscopoVazio_DevolveSucessoComZeroChamadasHttp()
    {
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(new EscopoLivroInteiroConsulta([], null)));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoColetaDePrecos.EscopoVazio, resultado.Value);
        Assert.Empty(ambiente.HubClient.Chamadas);
    }

    [Fact]
    public async Task Handle_EscopoSemPrecoAtualVazio_DevolveSucessoComZeroChamadasHttp()
    {
        var ambiente = CriarAmbiente(
            semPrecoAtual: () => Result<IReadOnlyList<string>>.Success(Array.Empty<string>()));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.SemPrecoAtualSoHoje, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(ResultadoColetaDePrecos.EscopoVazio, resultado.Value);
        Assert.Empty(ambiente.HubClient.Chamadas);
    }

    [Fact]
    public async Task Handle_SemPrecoAteAData_EInstrumentoDesconhecido_NoMesmoLote_TemDesfechosMetricasEGravacoesDiferentes()
    {
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:sem-preco", "td:desconhecido"], Hoje)),
            obterFatia: (_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(
            [
                new PrecoAsOfItem("td:sem-preco", null, "pu_venda", null, PrecoAsOfMotivo.SemPrecoAteAData),
                new PrecoAsOfItem("td:desconhecido", null, null, null, PrecoAsOfMotivo.InstrumentoDesconhecido),
            ]));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.SemPreco);
        Assert.Equal(1, resultado.Value.InstrumentosDesconhecidos);
        Assert.Empty(ambiente.PrecoWrite.HistoricosRegistrados);

        Assert.Contains(
            ambiente.Metrics.InstrumentosPosicionadosSemPreco,
            i => i.InstrumentoId == "td:sem-preco" && i.Motivo == "sem_preco_ate_a_data");
        Assert.Contains(ambiente.Metrics.InstrumentosDesconhecidos, id => id == "td:desconhecido");
        Assert.DoesNotContain("td:desconhecido", ambiente.Metrics.InstrumentosPosicionadosSemPreco.Select(i => i.InstrumentoId));
    }

    [Fact]
    public async Task Handle_CampoPosicaoTaxaVenda_ChamaRegistrarBootstrapComOCampoCerto()
    {
        var bootstraps = new List<ObservacaoDePreco>();

        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:tesouro-ipca-2035"], Hoje)),
            obterFatia: (_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(
            [
                new PrecoAsOfItem(
                    "td:tesouro-ipca-2035",
                    Hoje,
                    "taxa_venda",
                    new Dictionary<string, PrecoAsOfCampo> { ["taxa_venda"] = Campo(6.123456m, Hoje) },
                    PrecoAsOfMotivo.Nenhum),
            ]),
            registrarBootstrap: obs =>
            {
                bootstraps.Add(obs);
                return Result<ResultadoRegistroBootstrap>.Success(
                    new ResultadoRegistroBootstrap(ResultadoHistorico.Inserido(), ResultadoBootstrapPrecoAtual.Criado()));
            });

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var bootstrap = Assert.Single(bootstraps);
        Assert.Equal("taxa_venda", bootstrap.Campo);
        Assert.Equal(6.123456m, bootstrap.Valor);
        Assert.Equal(1, resultado.Value.PrecoAtualCriado);
    }

    [Fact]
    public async Task Handle_DataRefPorCampo_ProduzDuasObservacoesComDataRefDiferentes()
    {
        var dataDoCampoPosicao = Hoje;
        var dataDoOutroCampo = Hoje.AddDays(-3);

        var historicos = new List<ObservacaoDePreco>();
        var bootstraps = new List<ObservacaoDePreco>();

        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:tesouro-selic-2029"], Hoje)),
            obterFatia: (_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(
            [
                new PrecoAsOfItem(
                    "td:tesouro-selic-2029",
                    dataDoCampoPosicao,
                    "pu_venda",
                    new Dictionary<string, PrecoAsOfCampo>
                    {
                        ["pu_venda"] = Campo(105.5m, dataDoCampoPosicao),
                        ["taxa_venda"] = Campo(6.5m, dataDoOutroCampo),
                    },
                    PrecoAsOfMotivo.Nenhum),
            ]),
            registrarHistorico: obs =>
            {
                historicos.Add(obs);
                return Result<ResultadoHistorico>.Success(ResultadoHistorico.Inserido());
            },
            registrarBootstrap: obs =>
            {
                bootstraps.Add(obs);
                return Result<ResultadoRegistroBootstrap>.Success(
                    new ResultadoRegistroBootstrap(ResultadoHistorico.Inserido(), ResultadoBootstrapPrecoAtual.Criado()));
            });

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var historicoTaxaVenda = Assert.Single(historicos);
        var bootstrapPuVenda = Assert.Single(bootstraps);

        Assert.Equal("taxa_venda", historicoTaxaVenda.Campo);
        Assert.Equal(dataDoOutroCampo, historicoTaxaVenda.DataRef);

        Assert.Equal("pu_venda", bootstrapPuVenda.Campo);
        Assert.Equal(dataDoCampoPosicao, bootstrapPuVenda.DataRef);

        Assert.NotEqual(historicoTaxaVenda.DataRef, bootstrapPuVenda.DataRef);
    }

    [Fact]
    public async Task Handle_CampoPosicaoNulo_NuncaChamaRegistrarBootstrap_EGravaHistoricoDeTodosOsCampos()
    {
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:classe-desconhecida"], Hoje)),
            obterFatia: (_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(
            [
                new PrecoAsOfItem(
                    "td:classe-desconhecida",
                    Hoje,
                    null,
                    new Dictionary<string, PrecoAsOfCampo> { ["pu_venda"] = Campo(10m, Hoje) },
                    PrecoAsOfMotivo.Nenhum),
            ]),
            registrarBootstrap: _ => throw new InvalidOperationException(
                "RegistrarBootstrapAsync não pode ser chamado quando campoPosicao é nulo."));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.CampoPosicaoNaoInformado);
        Assert.Single(ambiente.PrecoWrite.HistoricosRegistrados);
        Assert.Contains("td:classe-desconhecida", ambiente.Metrics.CamposPosicaoNaoInformados);
    }

    [Fact]
    public async Task Handle_CamposSemAChaveDoCampoPosicao_NuncaChamaRegistrarBootstrap_EGravaHistoricoDosPresentes()
    {
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:tesouro-selic-2029"], Hoje)),
            obterFatia: (_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(
            [
                new PrecoAsOfItem(
                    "td:tesouro-selic-2029",
                    Hoje,
                    "taxa_venda",
                    new Dictionary<string, PrecoAsOfCampo> { ["pu_venda"] = Campo(105m, Hoje) },
                    PrecoAsOfMotivo.Nenhum),
            ]),
            registrarBootstrap: _ => throw new InvalidOperationException(
                "RegistrarBootstrapAsync não pode ser chamado quando o campoPosicao não está entre os campos devolvidos."));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.CampoPosicaoSemPreco);
        Assert.Single(ambiente.PrecoWrite.HistoricosRegistrados);
        Assert.Contains(
            ambiente.Metrics.InstrumentosPosicionadosSemPreco,
            i => i.InstrumentoId == "td:tesouro-selic-2029" && i.Motivo == "campo_posicao_sem_preco");
    }

    [Fact]
    public async Task Handle_ComRevisoesMaioresQueZero_EmiteUmResumoEZeroChamadasARegistrarRevisaoDePrecoRecebida()
    {
        var ambiente = CriarAmbiente(
            livroInteiro: () => Result<EscopoLivroInteiroConsulta>.Success(
                new EscopoLivroInteiroConsulta(["td:tesouro-selic-2029"], Hoje)),
            obterFatia: (_, _) => Result<IReadOnlyList<PrecoAsOfItem>>.Success(
            [
                new PrecoAsOfItem(
                    "td:tesouro-selic-2029",
                    Hoje,
                    "pu_venda",
                    new Dictionary<string, PrecoAsOfCampo> { ["pu_venda"] = Campo(105m, Hoje, revisao: 2) },
                    PrecoAsOfMotivo.Nenhum),
            ]),
            registrarBootstrap: _ => Result<ResultadoRegistroBootstrap>.Success(
                new ResultadoRegistroBootstrap(ResultadoHistorico.Inserido(), ResultadoBootstrapPrecoAtual.Criado())));

        var resultado = await ambiente.Handler.Handle(
            new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.RevisoesMaioresQueZero);
        Assert.Empty(ambiente.Metrics.RevisoesDePrecoRecebidas);
        Assert.Contains(
            ambiente.Logger.Entries,
            e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning
                && e.Message.Contains("revis", StringComparison.OrdinalIgnoreCase));
    }
}

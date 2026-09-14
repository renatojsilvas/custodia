using System.Net;
using Custodia.Application.Precos.Hub;
using Custodia.Domain.Common;
using Custodia.Infrastructure.Hub;
using Custodia.Infrastructure.Tests.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Custodia.Infrastructure.Tests.Hub;

public sealed class HubPrecosClientTests
{
    private const string BaseUrl = "http://hub.internal/";
    private static readonly DateOnly Data = new(2026, 9, 10);

    private static HubPrecosClient CriarCliente(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) }, NullLogger<HubPrecosClient>.Instance);

    private static HubPrecosClient CriarCliente(FakeHttpMessageHandler handler, FakeLogger<HubPrecosClient> logger) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) }, logger);

    private static string ItemJson(
        string instrumentoId,
        string dataRef = "2026-09-10",
        string campoPosicao = "pu_venda",
        string campo = "pu_venda",
        string valor = "105.123456",
        string fonte = "td",
        int revisao = 0,
        string observadoEm = "2026-09-10T20:00:00Z") =>
        "{\"instrumentoId\":\"" + instrumentoId + "\",\"dataRef\":\"" + dataRef + "\",\"campoPosicao\":\"" + campoPosicao +
        "\",\"campos\":{\"" + campo + "\":{\"valor\":\"" + valor + "\",\"fonte\":\"" + fonte + "\",\"revisao\":" + revisao +
        ",\"observadoEm\":\"" + observadoEm + "\",\"dataRef\":\"" + dataRef + "\"}},\"motivo\":null}";

    [Fact]
    public async Task ObterFatiaAsync_MontaCaminhoDateInstrumentsEscapadoPageEPageSize()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            $$"""{"date":"2026-09-10","items":[{{ItemJson("td:tesouro selic 2029")}}]}""",
            new Dictionary<string, string> { ["X-Total-Count"] = "1" }));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro selic 2029"], CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var uri = Assert.Single(handler.RequestedUris);
        Assert.StartsWith($"{BaseUrl}v1/prices/asof?", uri.ToString(), StringComparison.Ordinal);
        Assert.Contains("date=2026-09-10", uri.Query, StringComparison.Ordinal);
        Assert.Contains("instruments=td%3Atesouro%20selic%202029", uri.Query, StringComparison.Ordinal);
        Assert.Contains("page=1", uri.Query, StringComparison.Ordinal);
        Assert.Contains("pageSize=1", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComFatiaCheia_DevolveSucesso()
    {
        var ids = Enumerable.Range(0, 100).Select(i => $"td:instrumento-{i}").ToList();
        var itens = string.Join(",", ids.Select(id => ItemJson(id)));
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            $$"""{"date":"2026-09-10","items":[{{itens}}]}""",
            new Dictionary<string, string> { ["X-Total-Count"] = "100" }));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ids, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(100, resultado.Value.Count);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComExatamenteUmIdFaltandoNaResposta_DevolveColetaIncompleta()
    {
        var ids = Enumerable.Range(0, 5).Select(i => $"td:instrumento-{i}").ToList();
        var itens = string.Join(",", ids.Take(4).Select(id => ItemJson(id)));
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK, $$"""{"date":"2026-09-10","items":[{{itens}}]}"""));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ids, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubColetaIncompleta, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComIdDevolvidoQueNaoFoiPedido_DevolveColetaIncompleta()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            $$"""{"date":"2026-09-10","items":[{{ItemJson("td:outro-instrumento")}}]}"""));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubColetaIncompleta, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComIdRepetidoNaResposta_DevolveColetaIncompleta()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            $$"""
            {"date":"2026-09-10","items":[{{ItemJson("td:tesouro-selic-2029")}},{{ItemJson("td:tesouro-selic-2029")}}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubColetaIncompleta, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComXTotalCountMaiorQueItensDevolvidos_DevolveColetaIncompleta()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            $$"""{"date":"2026-09-10","items":[{{ItemJson("td:tesouro-selic-2029")}}]}""",
            new Dictionary<string, string> { ["X-Total-Count"] = "2" }));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(
            Data, ["td:tesouro-selic-2029", "td:tesouro-ipca-2035"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubColetaIncompleta, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_SemXTotalCountEComConjuntoCompleto_DevolveSucesso()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK, $$"""{"date":"2026-09-10","items":[{{ItemJson("td:tesouro-selic-2029")}}]}"""));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value);
    }

    [Fact]
    public async Task ObterFatiaAsync_NuncaEnviaIfNoneMatchMesmoNaSegundaChamadaAMesmaUrl()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue(FakeHttpMessageHandler.JsonResponse(
                HttpStatusCode.OK, $$"""{"date":"2026-09-10","items":[{{ItemJson("td:tesouro-selic-2029")}}]}"""))
            .Enqueue(FakeHttpMessageHandler.JsonResponse(
                HttpStatusCode.OK, $$"""{"date":"2026-09-10","items":[{{ItemJson("td:tesouro-selic-2029")}}]}"""));

        var cliente = CriarCliente(handler);

        await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);
        await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.False(r.Headers.Contains("If-None-Match")));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ObterFatiaAsync_Com401Ou403_DevolveAcessoNegado(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler().Enqueue(new HttpResponseMessage(status));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubAcessoNegado, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_Com500_DevolveIndisponivel()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubIndisponivel, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComExcecaoDeTransporte_DevolveIndisponivel()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(_ => throw new HttpRequestException("conexão recusada"));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubIndisponivel, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComTimeout_DevolveIndisponivel()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(
            _ => throw new TaskCanceledException("timeout", new TimeoutException()));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubIndisponivel, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_Com400_DevolveRespostaInvalida()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubRespostaInvalida, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComCorpoIlegivel_DevolveFalhaDeTransporteOuDeContrato()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.BrokenBodyResponse());

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Contains(
            resultado.Error,
            new[] { HubPrecosErrors.HubIndisponivel, HubPrecosErrors.HubRespostaInvalida });
    }

    [Fact]
    public async Task ObterFatiaAsync_ComValorIlegivel_DevolveRespostaInvalida()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """
            {"date":"2026-09-10","items":[{"instrumentoId":"td:tesouro-selic-2029","dataRef":"2026-09-10",
             "campoPosicao":"pu_venda","campos":{"pu_venda":{"valor":"não-é-decimal","fonte":"td","revisao":0,
             "observadoEm":"2026-09-10T20:00:00Z","dataRef":"2026-09-10"}},"motivo":null}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubRespostaInvalida, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComMotivoDesconhecido_DevolveRespostaInvalida()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """
            {"date":"2026-09-10","items":[{"instrumentoId":"td:tesouro-selic-2029","dataRef":null,
             "campoPosicao":null,"campos":null,"motivo":"motivo_nunca_visto"}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubRespostaInvalida, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComItemSemCampoObrigatorio_DevolveRespostaInvalida()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """
            {"date":"2026-09-10","items":[{"instrumentoId":"td:tesouro-selic-2029","dataRef":"2026-09-10",
             "campoPosicao":null,"campos":null,"motivo":null}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubRespostaInvalida, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComJsonIlegivel_DevolveRespostaInvalida()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK, "{ isso não é json"));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(HubPrecosErrors.HubRespostaInvalida, resultado.Error);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComTokenJaCancelado_Lanca()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(_ => throw new OperationCanceledException());
        var cliente = CriarCliente(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], cts.Token));
    }

    [Fact]
    public async Task ObterFatiaAsync_ComCampoPosicaoTaxaVenda_TipaCorretamenteOCampo()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            $$"""
            {"date":"2026-09-10","items":[{{ItemJson("td:tesouro-ipca-2035", campoPosicao: "taxa_venda", campo: "taxa_venda", valor: "6.123456")}}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-ipca-2035"], CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var item = Assert.Single(resultado.Value);
        Assert.Equal("taxa_venda", item.CampoPosicao);
        Assert.Equal(6.123456m, item.Campos!["taxa_venda"].Valor);
        Assert.Equal(PrecoAsOfMotivo.Nenhum, item.Motivo);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComMotivoSemPrecoAteAData_TipaOMotivoCorretamente()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """
            {"date":"2026-09-10","items":[{"instrumentoId":"td:tesouro-selic-2029","dataRef":null,
             "campoPosicao":"pu_venda","campos":null,"motivo":"sem_preco_ate_a_data"}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:tesouro-selic-2029"], CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var item = Assert.Single(resultado.Value);
        Assert.Equal(PrecoAsOfMotivo.SemPrecoAteAData, item.Motivo);
        Assert.Null(item.DataRef);
    }

    [Fact]
    public async Task ObterFatiaAsync_ComMotivoInstrumentoDesconhecido_TipaOMotivoCorretamente()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(FakeHttpMessageHandler.JsonResponse(
            HttpStatusCode.OK,
            """
            {"date":"2026-09-10","items":[{"instrumentoId":"td:desconhecido","dataRef":null,
             "campoPosicao":null,"campos":null,"motivo":"instrumento_desconhecido"}]}
            """));

        var cliente = CriarCliente(handler);

        var resultado = await cliente.ObterFatiaAsync(Data, ["td:desconhecido"], CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var item = Assert.Single(resultado.Value);
        Assert.Equal(PrecoAsOfMotivo.InstrumentoDesconhecido, item.Motivo);
        Assert.Null(item.CampoPosicao);
    }
}

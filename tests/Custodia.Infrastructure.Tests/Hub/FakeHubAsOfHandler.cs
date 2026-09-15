using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Custodia.Infrastructure.Tests.Hub;

internal sealed class FakeHubAsOfHandler : HttpMessageHandler
{
    public sealed record ObservacaoDoCatalogo(
        string InstrumentoId, DateOnly DataRef, string Campo, string Fonte, decimal Valor, int Revisao, DateTimeOffset ObservadoEm);

    private const string FormatoData = "yyyy-MM-dd";
    private const string FormatoObservadoEm = "yyyy-MM-ddTHH:mm:ssZ";

    private readonly Dictionary<string, string> _campoPosicaoPorInstrumento = new(StringComparer.Ordinal);
    private readonly List<ObservacaoDoCatalogo> _observacoes = [];

    public List<Uri> RequestedUris { get; } = [];

    public void RegistrarCatalogo(string instrumentoId, string campoPosicao) =>
        _campoPosicaoPorInstrumento[instrumentoId] = campoPosicao;

    public void RegistrarObservacao(ObservacaoDoCatalogo observacao) => _observacoes.Add(observacao);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!);

        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var data = DateOnly.ParseExact(query["date"]!, FormatoData, CultureInfo.InvariantCulture);
        var instrumentos = query["instruments"]!.Split(',');

        var items = new JsonArray();
        foreach (var instrumentoId in instrumentos)
        {
            items.Add(ConstruirItem(instrumentoId, data));
        }

        var corpo = new JsonObject
        {
            ["date"] = data.ToString(FormatoData, CultureInfo.InvariantCulture),
            ["items"] = items,
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(corpo.ToJsonString()),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        response.Headers.TryAddWithoutValidation("X-Total-Count", instrumentos.Length.ToString(CultureInfo.InvariantCulture));

        return Task.FromResult(response);
    }

    private JsonObject ConstruirItem(string instrumentoId, DateOnly data)
    {
        if (!_campoPosicaoPorInstrumento.TryGetValue(instrumentoId, out var campoPosicao))
        {
            return new JsonObject
            {
                ["instrumentoId"] = instrumentoId,
                ["dataRef"] = null,
                ["campoPosicao"] = null,
                ["campos"] = null,
                ["motivo"] = "instrumento_desconhecido",
            };
        }

        var camposPresentes = _observacoes
            .Where(o => o.InstrumentoId == instrumentoId)
            .Select(o => o.Campo)
            .Distinct()
            .ToList();

        var correntesPorCampo = new Dictionary<string, ObservacaoDoCatalogo>();

        foreach (var campo in camposPresentes)
        {
            var candidatos = _observacoes
                .Where(o => o.InstrumentoId == instrumentoId && o.Campo == campo && o.DataRef <= data)
                .ToList();

            if (candidatos.Count == 0)
            {
                continue;
            }

            var maiorData = candidatos.Max(o => o.DataRef);
            var corrente = candidatos
                .Where(o => o.DataRef == maiorData)
                .OrderByDescending(o => o.Revisao)
                .First();

            correntesPorCampo[campo] = corrente;
        }

        if (correntesPorCampo.Count == 0)
        {
            return new JsonObject
            {
                ["instrumentoId"] = instrumentoId,
                ["dataRef"] = null,
                ["campoPosicao"] = campoPosicao,
                ["campos"] = null,
                ["motivo"] = "sem_preco_ate_a_data",
            };
        }

        var camposJson = new JsonObject();
        foreach (var (campo, corrente) in correntesPorCampo)
        {
            camposJson[campo] = new JsonObject
            {
                ["valor"] = corrente.Valor.ToString(CultureInfo.InvariantCulture),
                ["fonte"] = corrente.Fonte,
                ["revisao"] = corrente.Revisao,
                ["observadoEm"] = corrente.ObservadoEm.UtcDateTime.ToString(FormatoObservadoEm, CultureInfo.InvariantCulture),
                ["dataRef"] = corrente.DataRef.ToString(FormatoData, CultureInfo.InvariantCulture),
            };
        }

        var maiorDataRefEntreCampos = correntesPorCampo.Values.Max(o => o.DataRef);

        return new JsonObject
        {
            ["instrumentoId"] = instrumentoId,
            ["dataRef"] = maiorDataRefEntreCampos.ToString(FormatoData, CultureInfo.InvariantCulture),
            ["campoPosicao"] = campoPosicao,
            ["campos"] = camposJson,
            ["motivo"] = null,
        };
    }
}

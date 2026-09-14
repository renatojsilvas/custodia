using System.Globalization;
using System.Net;
using System.Text.Json;
using Custodia.Application.Precos.Hub;
using Custodia.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Custodia.Infrastructure.Hub;

public sealed class HubPrecosClient(HttpClient httpClient, ILogger<HubPrecosClient> logger) : IHubPrecosClient
{
    private const string AsOfPath = "v1/prices/asof";
    private const string FormatoData = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<PrecoAsOfItem>>> ObterFatiaAsync(
        DateOnly data, IReadOnlyList<string> instrumentos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instrumentos);
        if (instrumentos.Count == 0)
        {
            throw new ArgumentException("A fatia de instrumentos não pode ser vazia.", nameof(instrumentos));
        }

        var instrumentosDeduplicados = instrumentos.Distinct(StringComparer.Ordinal).ToList();
        var requestUri = BuildRequestUri(data, instrumentosDeduplicados);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha de transporte ao consultar {RequestUri} no Hub de Preços.", requestUri);
            return HubPrecosErrors.HubIndisponivel;
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogError(
                    "Hub de Preços recusou a chamada a {RequestUri} com {StatusCode}.",
                    requestUri, (int)response.StatusCode);
                return HubPrecosErrors.HubAcessoNegado;
            }

            if ((int)response.StatusCode >= 500)
            {
                logger.LogError(
                    "Hub de Preços respondeu {StatusCode} para {RequestUri}.", (int)response.StatusCode, requestUri);
                return HubPrecosErrors.HubIndisponivel;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Hub de Preços respondeu {StatusCode} para {RequestUri}.", (int)response.StatusCode, requestUri);
                return HubPrecosErrors.HubRespostaInvalida;
            }

            int? totalCount = null;
            if (response.Headers.TryGetValues("X-Total-Count", out var totalCountValues)
                && int.TryParse(
                    totalCountValues.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
            {
                totalCount = total;
            }

            HubAsOfResponse? corpo;
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                corpo = await JsonSerializer.DeserializeAsync<HubAsOfResponse>(stream, JsonOptions, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao ler/desserializar a resposta do Hub de Preços para {RequestUri}.", requestUri);
                return HubPrecosErrors.HubRespostaInvalida;
            }

            if (corpo?.Items is null)
            {
                logger.LogError("Resposta do Hub de Preços para {RequestUri} não trouxe 'items'.", requestUri);
                return HubPrecosErrors.HubRespostaInvalida;
            }

            if (totalCount is int totalAnunciado && totalAnunciado > corpo.Items.Count)
            {
                logger.LogError(
                    "Hub de Preços anunciou X-Total-Count {TotalAnunciado} maior que os {Itens} itens devolvidos " +
                    "para {RequestUri} — truncamento por página.",
                    totalAnunciado, corpo.Items.Count, requestUri);
                return HubPrecosErrors.HubColetaIncompleta;
            }

            var itensTipados = new List<PrecoAsOfItem>(corpo.Items.Count);
            foreach (var itemBruto in corpo.Items)
            {
                var itemResult = ParaItem(itemBruto);
                if (itemResult.IsFailure)
                {
                    logger.LogError(
                        "Item da resposta do Hub de Preços para {RequestUri} é inválido: {Motivo}.",
                        requestUri, itemResult.Error.Description);
                    return itemResult.Error;
                }

                itensTipados.Add(itemResult.Value);
            }

            var idsPedidos = new HashSet<string>(instrumentosDeduplicados, StringComparer.Ordinal);
            var idsDevolvidos = new HashSet<string>(StringComparer.Ordinal);
            var idRepetido = false;

            foreach (var item in itensTipados)
            {
                if (!idsDevolvidos.Add(item.InstrumentoId))
                {
                    idRepetido = true;
                }
            }

            if (idRepetido)
            {
                logger.LogError("Hub de Preços devolveu id de instrumento repetido na resposta de {RequestUri}.", requestUri);
                return HubPrecosErrors.HubColetaIncompleta;
            }

            var faltando = idsPedidos.Except(idsDevolvidos).ToList();
            if (faltando.Count > 0)
            {
                logger.LogError(
                    "Hub de Preços não devolveu {Quantidade} id(s) pedido(s) em {RequestUri}: {Ids}.",
                    faltando.Count, requestUri, string.Join(",", faltando));
                return HubPrecosErrors.HubColetaIncompleta;
            }

            var idsNaoPedidos = idsDevolvidos.Except(idsPedidos).ToList();
            if (idsNaoPedidos.Count > 0)
            {
                logger.LogError(
                    "Hub de Preços devolveu {Quantidade} id(s) não pedido(s) em {RequestUri} (sintoma de id do " +
                    "livro fora de trim+minúsculas): {Ids}.",
                    idsNaoPedidos.Count, requestUri, string.Join(",", idsNaoPedidos));
                return HubPrecosErrors.HubColetaIncompleta;
            }

            return Result<IReadOnlyList<PrecoAsOfItem>>.Success(itensTipados);
        }
    }

    private static Result<PrecoAsOfItem> ParaItem(HubAsOfItemResponse bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto.InstrumentoId))
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        var motivoResult = ParaMotivo(bruto.Motivo);
        if (motivoResult.IsFailure)
        {
            return motivoResult.Error;
        }

        DateOnly? dataRef = null;
        if (bruto.DataRef is not null)
        {
            if (!DateOnly.TryParseExact(
                    bruto.DataRef, FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataRefValor))
            {
                return HubPrecosErrors.HubRespostaInvalida;
            }

            dataRef = dataRefValor;
        }

        IReadOnlyDictionary<string, PrecoAsOfCampo>? campos = null;
        if (bruto.Campos is not null)
        {
            var camposConvertidos = new Dictionary<string, PrecoAsOfCampo>();
            foreach (var (campo, campoBruto) in bruto.Campos)
            {
                if (string.IsNullOrWhiteSpace(campo) || campo.Trim() != campo)
                {
                    return HubPrecosErrors.HubRespostaInvalida;
                }

                var campoResult = ParaCampo(campoBruto);
                if (campoResult.IsFailure)
                {
                    return campoResult.Error;
                }

                camposConvertidos[campo] = campoResult.Value;
            }

            campos = camposConvertidos;
        }

        if (motivoResult.Value == PrecoAsOfMotivo.Nenhum && (campos is null || campos.Count == 0))
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        return new PrecoAsOfItem(bruto.InstrumentoId, dataRef, bruto.CampoPosicao, campos, motivoResult.Value);
    }

    private static Result<PrecoAsOfCampo> ParaCampo(HubAsOfCampoResponse? campoBruto)
    {
        if (campoBruto is null)
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        if (!decimal.TryParse(
                campoBruto.Valor, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var valor)
            || SchemaNumericLimits.ExcedePrecisaoDePreco(valor))
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        if (string.IsNullOrWhiteSpace(campoBruto.Fonte) || campoBruto.Fonte.Trim() != campoBruto.Fonte)
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        if (campoBruto.DataRef is null
            || !DateOnly.TryParseExact(
                campoBruto.DataRef, FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataRef))
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        if (campoBruto.ObservadoEm is null
            || !DateTimeOffset.TryParse(
                campoBruto.ObservadoEm, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var observadoEm))
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        if (campoBruto.Revisao < 0)
        {
            return HubPrecosErrors.HubRespostaInvalida;
        }

        return new PrecoAsOfCampo(valor, campoBruto.Fonte, campoBruto.Revisao, observadoEm, dataRef);
    }

    private static Result<PrecoAsOfMotivo> ParaMotivo(string? motivo) => motivo switch
    {
        null => PrecoAsOfMotivo.Nenhum,
        "sem_preco_ate_a_data" => PrecoAsOfMotivo.SemPrecoAteAData,
        "instrumento_desconhecido" => PrecoAsOfMotivo.InstrumentoDesconhecido,
        _ => HubPrecosErrors.HubRespostaInvalida,
    };

    private static string BuildRequestUri(DateOnly data, IReadOnlyList<string> instrumentos)
    {
        var dataTexto = data.ToString(FormatoData, CultureInfo.InvariantCulture);
        var instrumentsTexto = string.Join(",", instrumentos.Select(Uri.EscapeDataString));

        return $"{AsOfPath}?date={dataTexto}&instruments={instrumentsTexto}&page=1&pageSize={instrumentos.Count}";
    }
}

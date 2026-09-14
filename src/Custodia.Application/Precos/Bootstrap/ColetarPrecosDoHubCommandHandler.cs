using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Precos.Hub;
using Custodia.Domain.Common;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Custodia.Application.Precos.Bootstrap;

public sealed class ColetarPrecosDoHubCommandHandler(
    IEscopoDePrecosReadRepository escopoDePrecosReadRepository,
    ICalendarioDiasUteisReadRepository calendarioDiasUteisReadRepository,
    IHubPrecosClient hubPrecosClient,
    IPrecoWriteRepository precoWriteRepository,
    IUnitOfWork unitOfWork,
    IBusinessMetrics businessMetrics,
    IConfiguration configuration,
    ILogger<ColetarPrecosDoHubCommandHandler> logger)
    : IRequestHandler<ColetarPrecosDoHubCommand, Result<ResultadoColetaDePrecos>>
{
    public const int TetoDiasPadrao = 400;
    public const int TamanhoFatiaPadrao = 100;
    public const int TetoFatiasPadrao = 50;

    private const string ChaveTetoDias = "Decisao:BootstrapPrecosTetoDias";
    private const string ChaveTamanhoFatia = "Decisao:BootstrapPrecosTamanhoFatia";
    private const string ChaveTetoFatias = "Decisao:BootstrapPrecosTetoFatias";
    private const string MotivoPosicionadoSemPrecoAteAData = "sem_preco_ate_a_data";
    private const string MotivoPosicionadoCampoPosicaoSemPreco = "campo_posicao_sem_preco";

    private readonly int _tetoDias = configuration.GetValue<int?>(ChaveTetoDias) ?? TetoDiasPadrao;
    private readonly int _tamanhoFatia = configuration.GetValue<int?>(ChaveTamanhoFatia) ?? TamanhoFatiaPadrao;
    private readonly int _tetoFatias = configuration.GetValue<int?>(ChaveTetoFatias) ?? TetoFatiasPadrao;

    private sealed class Contadores
    {
        public int HistoricoInserido;
        public int PrecoAtualCriado;
        public int PrecoAtualAtualizado;
        public int PrecoAtualCampoTrocado;
        public int SemPreco;
        public int CampoPosicaoSemPreco;
        public int CampoPosicaoNaoInformado;
        public int InstrumentosDesconhecidos;
        public int ValoresDivergentes;
        public int RevisoesMaioresQueZero;
    }

    public async Task<Result<ResultadoColetaDePrecos>> Handle(ColetarPrecosDoHubCommand request, CancellationToken ct)
    {
        var escopoResult = await ObterEscopoAsync(request, ct);
        if (escopoResult.IsFailure)
        {
            return escopoResult.Error;
        }

        var (instrumentosBrutos, desde, ate) = escopoResult.Value;
        var instrumentos = instrumentosBrutos.Distinct().ToList();

        if (instrumentos.Count == 0)
        {
            return ResultadoColetaDePrecos.EscopoVazio;
        }

        if (desde > ate)
        {
            return ColetaDePrecosErrors.DesdeMaiorQueAte;
        }

        var totalDias = ate.DayNumber - desde.DayNumber + 1;
        if (totalDias > _tetoDias)
        {
            logger.LogError(
                "Coleta de preços do Hub abortada antes de qualquer chamada: janela de {TotalDias} dias excede " +
                "o teto configurado {TetoDias}.", totalDias, _tetoDias);
            return ColetaDePrecosErrors.TetoDeDiasExcedido;
        }

        var fatias = Fatiar(instrumentos, _tamanhoFatia);
        if (fatias.Count > _tetoFatias)
        {
            logger.LogError(
                "Coleta de preços do Hub abortada antes de qualquer chamada: {TotalFatias} fatias de instrumentos " +
                "excedem o teto configurado {TetoFatias}.", fatias.Count, _tetoFatias);
            return ColetaDePrecosErrors.TetoDeFatiasExcedido;
        }

        var contadores = new Contadores();
        var chamadasHttp = 0;
        var diasProcessados = 0;

        for (var dataAtual = desde; dataAtual <= ate; dataAtual = dataAtual.AddDays(1))
        {
            foreach (var fatia in fatias)
            {
                var fatiaResult = await hubPrecosClient.ObterFatiaAsync(dataAtual, fatia, ct);
                chamadasHttp++;

                if (fatiaResult.IsFailure)
                {
                    return fatiaResult.Error;
                }

                foreach (var item in fatiaResult.Value)
                {
                    var itemResult = await ProcessarItemAsync(item, contadores, ct);
                    if (itemResult.IsFailure)
                    {
                        await unitOfWork.DescartarTransacaoAsync(ct);
                        return itemResult.Error;
                    }
                }

                var saveResult = await unitOfWork.SaveChangesAsync(ct);
                if (saveResult.IsFailure)
                {
                    return saveResult.Error;
                }
            }

            diasProcessados++;
        }

        if (contadores.RevisoesMaioresQueZero > 0)
        {
            logger.LogWarning(
                "Coleta de preços do Hub recebeu {Quantidade} revisão(ões) de preço (revisao > 0) nesta execução; " +
                "correções são raras (ARQUITETURA §12).",
                contadores.RevisoesMaioresQueZero);
        }

        return new ResultadoColetaDePrecos(
            diasProcessados,
            chamadasHttp,
            instrumentos.Count,
            contadores.HistoricoInserido,
            contadores.PrecoAtualCriado,
            contadores.PrecoAtualAtualizado,
            contadores.PrecoAtualCampoTrocado,
            contadores.SemPreco,
            contadores.CampoPosicaoSemPreco,
            contadores.CampoPosicaoNaoInformado,
            contadores.InstrumentosDesconhecidos,
            contadores.ValoresDivergentes,
            contadores.RevisoesMaioresQueZero);
    }

    private async Task<Result<(IReadOnlyList<string> Instrumentos, DateOnly Desde, DateOnly Ate)>> ObterEscopoAsync(
        ColetarPrecosDoHubCommand request, CancellationToken ct)
    {
        var horizonteResult = await calendarioDiasUteisReadRepository.ObterHorizonteAsync(ct);
        if (horizonteResult.IsFailure)
        {
            return horizonteResult.Error;
        }

        var hoje = horizonteResult.Value.Hoje;

        if (request.Escopo == EscopoDeColetaDePrecos.SemPrecoAtualSoHoje)
        {
            var semPrecoAtualResult = await escopoDePrecosReadRepository.ObterSemPrecoAtualAsync(ct);
            if (semPrecoAtualResult.IsFailure)
            {
                return semPrecoAtualResult.Error;
            }

            return (semPrecoAtualResult.Value, hoje, hoje);
        }

        var livroResult = await escopoDePrecosReadRepository.ObterLivroInteiroAsync(ct);
        if (livroResult.IsFailure)
        {
            return livroResult.Error;
        }

        var desde = request.Desde ?? livroResult.Value.MenorDataEvento ?? hoje;
        var ate = request.Ate ?? hoje;

        return (livroResult.Value.InstrumentosId, desde, ate);
    }

    private async Task<Result> ProcessarItemAsync(PrecoAsOfItem item, Contadores contadores, CancellationToken ct)
    {
        switch (item.Motivo)
        {
            case PrecoAsOfMotivo.InstrumentoDesconhecido:
                contadores.InstrumentosDesconhecidos++;
                logger.LogError(
                    "Instrumento {InstrumentoId} do livro é desconhecido no Hub ao consultar /v1/prices/asof.",
                    item.InstrumentoId);
                businessMetrics.RegistrarPrecoInstrumentoDesconhecido(item.InstrumentoId);
                return Result.Success();

            case PrecoAsOfMotivo.SemPrecoAteAData:
                contadores.SemPreco++;
                businessMetrics.RegistrarPrecoInstrumentoPosicionadoSemPreco(
                    item.InstrumentoId, MotivoPosicionadoSemPrecoAteAData);
                return Result.Success();

            case PrecoAsOfMotivo.Nenhum:
                return await ProcessarItemComPrecoAsync(item, contadores, ct);

            default:
                throw new InvalidOperationException($"Motivo de item asof não reconhecido: '{item.Motivo}'.");
        }
    }

    private async Task<Result> ProcessarItemComPrecoAsync(PrecoAsOfItem item, Contadores contadores, CancellationToken ct)
    {
        var campos = item.Campos!;

        if (item.CampoPosicao is null)
        {
            contadores.CampoPosicaoNaoInformado++;
            logger.LogError(
                "Instrumento {InstrumentoId} veio do Hub com campos de preço mas sem campoPosicao; gravado só o " +
                "histórico, preco_atual não foi tocado.",
                item.InstrumentoId);
            businessMetrics.RegistrarPrecoCampoPosicaoNaoInformado(item.InstrumentoId);
            return await GravarHistoricoDeTodosOsCamposAsync(item.InstrumentoId, campos, contadores, ct);
        }

        if (!campos.ContainsKey(item.CampoPosicao))
        {
            contadores.CampoPosicaoSemPreco++;
            businessMetrics.RegistrarPrecoInstrumentoPosicionadoSemPreco(
                item.InstrumentoId, MotivoPosicionadoCampoPosicaoSemPreco);
            return await GravarHistoricoDeTodosOsCamposAsync(item.InstrumentoId, campos, contadores, ct);
        }

        foreach (var (campo, campoDto) in campos)
        {
            if (campo == item.CampoPosicao)
            {
                continue;
            }

            var resultado = await RegistrarHistoricoCampoAsync(item.InstrumentoId, campo, campoDto, contadores, ct);
            if (resultado.IsFailure)
            {
                return resultado;
            }
        }

        return await RegistrarBootstrapCampoAsync(
            item.InstrumentoId, item.CampoPosicao, campos[item.CampoPosicao], contadores, ct);
    }

    private async Task<Result> GravarHistoricoDeTodosOsCamposAsync(
        string instrumentoId, IReadOnlyDictionary<string, PrecoAsOfCampo> campos, Contadores contadores, CancellationToken ct)
    {
        foreach (var (campo, campoDto) in campos)
        {
            var resultado = await RegistrarHistoricoCampoAsync(instrumentoId, campo, campoDto, contadores, ct);
            if (resultado.IsFailure)
            {
                return resultado;
            }
        }

        return Result.Success();
    }

    private async Task<Result> RegistrarHistoricoCampoAsync(
        string instrumentoId, string campo, PrecoAsOfCampo campoDto, Contadores contadores, CancellationToken ct)
    {
        var observacao = new ObservacaoDePreco(
            instrumentoId, campoDto.DataRef, campo, campoDto.Fonte, campoDto.Valor, campoDto.Revisao, campoDto.ObservadoEm);

        var resultado = await precoWriteRepository.RegistrarHistoricoAsync(observacao, ct);
        if (resultado.IsFailure)
        {
            return Result.Failure(resultado.Error);
        }

        ClassificarHistorico(resultado.Value, instrumentoId, campo, campoDto, contadores);
        return Result.Success();
    }

    private async Task<Result> RegistrarBootstrapCampoAsync(
        string instrumentoId, string campo, PrecoAsOfCampo campoDto, Contadores contadores, CancellationToken ct)
    {
        var observacao = new ObservacaoDePreco(
            instrumentoId, campoDto.DataRef, campo, campoDto.Fonte, campoDto.Valor, campoDto.Revisao, campoDto.ObservadoEm);

        var resultado = await precoWriteRepository.RegistrarBootstrapAsync(observacao, ct);
        if (resultado.IsFailure)
        {
            return Result.Failure(resultado.Error);
        }

        ClassificarHistorico(resultado.Value.Historico, instrumentoId, campo, campoDto, contadores);

        if (resultado.Value.PrecoAtual is { } precoAtual)
        {
            switch (precoAtual.Tipo)
            {
                case ResultadoBootstrapPrecoAtualTipo.Criado:
                    contadores.PrecoAtualCriado++;
                    break;

                case ResultadoBootstrapPrecoAtualTipo.AtualizadoMesmoCampo:
                    contadores.PrecoAtualAtualizado++;
                    break;

                case ResultadoBootstrapPrecoAtualTipo.CampoTrocado:
                    contadores.PrecoAtualCampoTrocado++;
                    logger.LogWarning(
                        "preco_atual.campo trocado para o instrumento {InstrumentoId}: '{CampoAnterior}' -> '{CampoNovo}'.",
                        instrumentoId, precoAtual.CampoAnterior, campo);
                    break;

                case ResultadoBootstrapPrecoAtualTipo.IgnoradoMaisAntigo:
                    break;
            }
        }

        return Result.Success();
    }

    private void ClassificarHistorico(
        ResultadoHistorico historico, string instrumentoId, string campo, PrecoAsOfCampo campoDto, Contadores contadores)
    {
        switch (historico.Tipo)
        {
            case ResultadoHistoricoTipo.Inserido:
                contadores.HistoricoInserido++;
                break;

            case ResultadoHistoricoTipo.Divergente:
                businessMetrics.RegistrarValorDivergenteNoHistoricoDePrecos(
                    instrumentoId, campo, campoDto.DataRef, campoDto.Fonte, campoDto.Revisao,
                    historico.ValorAnterior!.Value, campoDto.Valor);
                contadores.ValoresDivergentes++;
                break;

            case ResultadoHistoricoTipo.ReplayInocuo:
                break;
        }

        if (campoDto.Revisao > 0)
        {
            contadores.RevisoesMaioresQueZero++;
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> Fatiar(IReadOnlyList<string> instrumentos, int tamanhoFatia)
    {
        var fatias = new List<IReadOnlyList<string>>();

        for (var i = 0; i < instrumentos.Count; i += tamanhoFatia)
        {
            fatias.Add(instrumentos.Skip(i).Take(tamanhoFatia).ToList());
        }

        return fatias;
    }
}

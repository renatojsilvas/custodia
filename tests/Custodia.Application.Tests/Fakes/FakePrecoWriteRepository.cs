using Custodia.Application.Precos;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakePrecoWriteRepository(
    Func<ObservacaoDePreco, Result<ResultadoHistorico>>? registrarHistorico = null,
    Func<ObservacaoDePreco, Result<ResultadoAtualizacaoPrecoAtual>>? atualizarPrecoAtual = null,
    Func<ObservacaoDePreco, Result<ValorRevisaoAnteriorConsulta>>? obterValorRevisaoAnterior = null,
    Func<ObservacaoDePreco, Result<ResultadoRegistroBootstrap>>? registrarBootstrap = null)
    : IPrecoWriteRepository
{
    private readonly Func<ObservacaoDePreco, Result<ResultadoHistorico>> _registrarHistorico =
        registrarHistorico ?? (_ => Result<ResultadoHistorico>.Success(ResultadoHistorico.Inserido()));

    private readonly Func<ObservacaoDePreco, Result<ResultadoAtualizacaoPrecoAtual>> _atualizarPrecoAtual =
        atualizarPrecoAtual ?? (_ => Result<ResultadoAtualizacaoPrecoAtual>.Success(ResultadoAtualizacaoPrecoAtual.Atualizado()));

    private readonly Func<ObservacaoDePreco, Result<ValorRevisaoAnteriorConsulta>> _obterValorRevisaoAnterior =
        obterValorRevisaoAnterior ?? (_ => Result<ValorRevisaoAnteriorConsulta>.Success(ValorRevisaoAnteriorConsulta.Inexistente));

    private readonly Func<ObservacaoDePreco, Result<ResultadoRegistroBootstrap>> _registrarBootstrap =
        registrarBootstrap ?? (_ => throw new InvalidOperationException("RegistrarBootstrapAsync não configurado no fake."));

    public List<ObservacaoDePreco> HistoricosRegistrados { get; } = [];

    public List<ObservacaoDePreco> PrecosAtuaisAtualizados { get; } = [];

    public Task<Result<ResultadoHistorico>> RegistrarHistoricoAsync(ObservacaoDePreco observacao, CancellationToken ct)
    {
        HistoricosRegistrados.Add(observacao);
        return Task.FromResult(_registrarHistorico(observacao));
    }

    public Task<Result<ResultadoAtualizacaoPrecoAtual>> AtualizarPrecoAtualAsync(ObservacaoDePreco observacao, CancellationToken ct)
    {
        PrecosAtuaisAtualizados.Add(observacao);
        return Task.FromResult(_atualizarPrecoAtual(observacao));
    }

    public Task<Result<ValorRevisaoAnteriorConsulta>> ObterValorRevisaoAnteriorAsync(ObservacaoDePreco observacao, CancellationToken ct) =>
        Task.FromResult(_obterValorRevisaoAnterior(observacao));

    public Task<Result<ResultadoRegistroBootstrap>> RegistrarBootstrapAsync(ObservacaoDePreco observacao, CancellationToken ct) =>
        Task.FromResult(_registrarBootstrap(observacao));
}

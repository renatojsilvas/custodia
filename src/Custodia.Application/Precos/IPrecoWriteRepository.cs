using Custodia.Domain.Common;

namespace Custodia.Application.Precos;

public interface IPrecoWriteRepository
{
    Task<Result<ResultadoHistorico>> RegistrarHistoricoAsync(ObservacaoDePreco observacao, CancellationToken ct);

    Task<Result<ResultadoAtualizacaoPrecoAtual>> AtualizarPrecoAtualAsync(ObservacaoDePreco observacao, CancellationToken ct);

    Task<Result<ValorRevisaoAnteriorConsulta>> ObterValorRevisaoAnteriorAsync(ObservacaoDePreco observacao, CancellationToken ct);

    Task<Result<ResultadoRegistroBootstrap>> RegistrarBootstrapAsync(ObservacaoDePreco observacao, CancellationToken ct);
}

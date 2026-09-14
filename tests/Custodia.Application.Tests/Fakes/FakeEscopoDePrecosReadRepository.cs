using Custodia.Application.Precos.Bootstrap;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeEscopoDePrecosReadRepository(
    Func<Result<EscopoLivroInteiroConsulta>>? livroInteiro = null,
    Func<Result<IReadOnlyList<string>>>? semPrecoAtual = null)
    : IEscopoDePrecosReadRepository
{
    private readonly Func<Result<EscopoLivroInteiroConsulta>> _livroInteiro =
        livroInteiro ?? (() => Result<EscopoLivroInteiroConsulta>.Success(new EscopoLivroInteiroConsulta([], null)));

    private readonly Func<Result<IReadOnlyList<string>>> _semPrecoAtual =
        semPrecoAtual ?? (() => Result<IReadOnlyList<string>>.Success(Array.Empty<string>()));

    public Task<Result<EscopoLivroInteiroConsulta>> ObterLivroInteiroAsync(CancellationToken ct) =>
        Task.FromResult(_livroInteiro());

    public Task<Result<IReadOnlyList<string>>> ObterSemPrecoAtualAsync(CancellationToken ct) =>
        Task.FromResult(_semPrecoAtual());
}

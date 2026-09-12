using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakePosicaoCorrenteReadRepository(
    IReadOnlyDictionary<(string ClienteId, string InstrumentoId), PosicaoTresColunas>? posicoesExistentes = null,
    Func<string, string, Result<PosicaoTresColunas>>? obter = null,
    Func<string?, string?, Result<IReadOnlyList<ChavePosicao>>>? chaves = null)
    : IPosicaoCorrenteReadRepository
{
    private readonly IReadOnlyDictionary<(string, string), PosicaoTresColunas> _posicoes =
        posicoesExistentes ?? new Dictionary<(string, string), PosicaoTresColunas>();

    private readonly Func<string, string, Result<PosicaoTresColunas>>? _obter = obter;

    private readonly Func<string?, string?, Result<IReadOnlyList<ChavePosicao>>>? _chaves = chaves;

    public Task<Result<PosicaoTresColunas>> ObterAsync(string clienteId, string instrumentoId, CancellationToken ct)
    {
        if (_obter is not null)
        {
            return Task.FromResult(_obter(clienteId, instrumentoId));
        }

        var estado = _posicoes.TryGetValue((clienteId, instrumentoId), out var encontrada)
            ? encontrada
            : PosicaoTresColunas.Zero;

        return Task.FromResult(Result<PosicaoTresColunas>.Success(estado));
    }

    public Task<Result<IReadOnlyList<ChavePosicao>>> ObterChavesAsync(
        string? clienteId, string? instrumentoId, CancellationToken ct)
    {
        if (_chaves is not null)
        {
            return Task.FromResult(_chaves(clienteId, instrumentoId));
        }

        IReadOnlyList<ChavePosicao> chavesExistentes = _posicoes.Keys
            .Where(chave => clienteId is null || chave.Item1 == clienteId)
            .Where(chave => instrumentoId is null || chave.Item2 == instrumentoId)
            .Select(chave => new ChavePosicao(chave.Item1, chave.Item2))
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ChavePosicao>>.Success(chavesExistentes));
    }
}

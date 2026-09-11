using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakePosicaoCorrenteReadRepository(
    IReadOnlyDictionary<(string ClienteId, string InstrumentoId), PosicaoTresColunas>? posicoesExistentes = null,
    Func<string, string, Result<PosicaoTresColunas>>? obter = null)
    : IPosicaoCorrenteReadRepository
{
    private readonly IReadOnlyDictionary<(string, string), PosicaoTresColunas> _posicoes =
        posicoesExistentes ?? new Dictionary<(string, string), PosicaoTresColunas>();

    private readonly Func<string, string, Result<PosicaoTresColunas>>? _obter = obter;

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
}

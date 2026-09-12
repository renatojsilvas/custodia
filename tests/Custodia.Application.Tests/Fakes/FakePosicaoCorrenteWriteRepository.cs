using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Tests.Fakes;

internal sealed record AtualizacaoDePosicao(string ClienteId, string InstrumentoId, PosicaoTresColunas Estado);

internal sealed class FakePosicaoCorrenteWriteRepository(
    Func<string, string, PosicaoTresColunas, Result>? atualizar = null,
    Func<string, string, Result>? remover = null)
    : IPosicaoCorrenteWriteRepository
{
    private readonly Func<string, string, PosicaoTresColunas, Result> _atualizar =
        atualizar ?? ((_, _, _) => Result.Success());

    private readonly Func<string, string, Result> _remover = remover ?? ((_, _) => Result.Success());

    public List<AtualizacaoDePosicao> Atualizacoes { get; } = [];

    public List<(string ClienteId, string InstrumentoId)> Removidas { get; } = [];

    public Task<Result> AtualizarAsync(
        string clienteId, string instrumentoId, PosicaoTresColunas estado, CancellationToken ct)
    {
        Atualizacoes.Add(new AtualizacaoDePosicao(clienteId, instrumentoId, estado));
        return Task.FromResult(_atualizar(clienteId, instrumentoId, estado));
    }

    public Task<Result> RemoverAsync(string clienteId, string instrumentoId, CancellationToken ct)
    {
        Removidas.Add((clienteId, instrumentoId));
        return Task.FromResult(_remover(clienteId, instrumentoId));
    }

    public PosicaoTresColunas? UltimaPosicaoDe(string clienteId, string instrumentoId) =>
        Atualizacoes.LastOrDefault(a => a.ClienteId == clienteId && a.InstrumentoId == instrumentoId)?.Estado;
}

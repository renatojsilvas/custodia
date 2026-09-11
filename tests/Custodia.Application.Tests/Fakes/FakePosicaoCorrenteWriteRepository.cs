using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Tests.Fakes;

internal sealed record AtualizacaoDePosicao(string ClienteId, string InstrumentoId, PosicaoTresColunas Estado);

internal sealed class FakePosicaoCorrenteWriteRepository(
    Func<string, string, PosicaoTresColunas, Result>? atualizar = null)
    : IPosicaoCorrenteWriteRepository
{
    private readonly Func<string, string, PosicaoTresColunas, Result> _atualizar =
        atualizar ?? ((_, _, _) => Result.Success());

    public List<AtualizacaoDePosicao> Atualizacoes { get; } = [];

    public Task<Result> AtualizarAsync(
        string clienteId, string instrumentoId, PosicaoTresColunas estado, CancellationToken ct)
    {
        Atualizacoes.Add(new AtualizacaoDePosicao(clienteId, instrumentoId, estado));
        return Task.FromResult(_atualizar(clienteId, instrumentoId, estado));
    }

    public PosicaoTresColunas? UltimaPosicaoDe(string clienteId, string instrumentoId) =>
        Atualizacoes.LastOrDefault(a => a.ClienteId == clienteId && a.InstrumentoId == instrumentoId)?.Estado;
}

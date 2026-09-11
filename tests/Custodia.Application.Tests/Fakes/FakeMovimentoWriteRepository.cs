using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeMovimentoWriteRepository(Func<Movimento, Result>? adicionar = null)
    : IMovimentoWriteRepository
{
    private readonly Func<Movimento, Result> _adicionar = adicionar ?? (_ => Result.Success());

    public List<Movimento> Adicionados { get; } = [];

    public Task<Result> AdicionarAsync(Movimento movimento, CancellationToken ct)
    {
        Adicionados.Add(movimento);
        return Task.FromResult(_adicionar(movimento));
    }
}

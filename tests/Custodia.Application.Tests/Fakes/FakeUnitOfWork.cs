using Custodia.Application.Common.Interfaces;
using Custodia.Domain.Common;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeUnitOfWork(Func<int, Result>? saveChanges = null) : IUnitOfWork
{
    private readonly Func<int, Result> _saveChanges = saveChanges ?? (_ => Result.Success());

    public int ChamadasDeSaveChanges { get; private set; }

    public int ChamadasDeLimparRastreamento { get; private set; }

    public Task<Result> SaveChangesAsync(CancellationToken ct)
    {
        ChamadasDeSaveChanges++;
        return Task.FromResult(_saveChanges(ChamadasDeSaveChanges));
    }

    public void LimparRastreamento() => ChamadasDeLimparRastreamento++;
}

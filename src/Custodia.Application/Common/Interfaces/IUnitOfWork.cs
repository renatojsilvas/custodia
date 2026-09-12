using Custodia.Domain.Common;

namespace Custodia.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<Result> SaveChangesAsync(CancellationToken ct);

    void LimparRastreamento();
}

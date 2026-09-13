using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Backfill;

public sealed record BackfillJanelaF4F5Command : IRequest<Result<BackfillJanelaF4F5Resultado>>;

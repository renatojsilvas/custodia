using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Conciliacao;

public sealed record ExecutarConciliacaoDeResgatesCommand : IRequest<Result<ResultadoConciliacaoDeResgates>>;

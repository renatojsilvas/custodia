using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Reparo;

public sealed record RepararResgatesAntigosCommand : IRequest<Result<RepararResgatesAntigosResultado>>;

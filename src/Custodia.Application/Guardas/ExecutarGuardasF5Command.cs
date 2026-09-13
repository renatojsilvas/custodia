using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Guardas;

public sealed record ExecutarGuardasF5Command : IRequest<Result<ResultadoGuardasF5>>;

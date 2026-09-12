using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Posicoes;

public sealed record ReconstruirPosicoesCommand(string? ClienteId, string? InstrumentoId)
    : IRequest<Result<ReconstruirPosicoesResultado>>;

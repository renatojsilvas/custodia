using Custodia.Domain.Common;
using MediatR;

namespace Custodia.Application.Precos.Bootstrap;

public sealed record ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos Escopo, DateOnly? Desde, DateOnly? Ate)
    : IRequest<Result<ResultadoColetaDePrecos>>;

using MediatR;
using Custodia.Domain.Common;

namespace Custodia.Application.Liquidacao;

public sealed record LiquidarResgatesVencidosCommand : IRequest<Result<ResultadoLiquidacaoDeResgates>>;

using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public interface IAplicadorIncrementalDePosicao
{
    Task<Result<PosicaoTresColunas>> AplicarAsync(EstadoDaGravacao lote, Movimento movimento, CancellationToken ct);
}

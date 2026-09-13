using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public sealed class EstadoDaGravacao
{
    public Dictionary<(string ClienteId, string InstrumentoId), PosicaoTresColunas> EstadosJaAplicados { get; } = [];

    public Dictionary<(string ClienteId, string InstrumentoId), List<Movimento>> MovimentosJaAdicionadosPorChave { get; } = [];
}

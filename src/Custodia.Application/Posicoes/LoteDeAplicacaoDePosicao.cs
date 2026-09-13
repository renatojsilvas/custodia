using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public sealed class LoteDeAplicacaoDePosicao
{
    public Dictionary<(string ClienteId, string InstrumentoId), PosicaoTresColunas> EstadosJaAplicados { get; } = [];

    public Dictionary<(string ClienteId, string InstrumentoId), List<Movimento>> MovimentosJaAdicionadosPorChave { get; } = [];
}

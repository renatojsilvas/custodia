using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public sealed class AplicadorIncrementalDePosicao(
    IMovimentoReadRepository movimentoReadRepository,
    IPosicaoCorrenteReadRepository posicaoCorrenteReadRepository)
    : IAplicadorIncrementalDePosicao
{
    public async Task<Result<PosicaoTresColunas>> AplicarAsync(
        LoteDeAplicacaoDePosicao lote, Movimento movimento, CancellationToken ct)
    {
        var chave = (movimento.ClienteId, movimento.InstrumentoId);

        var resultado = movimento.Tipo == TipoMovimento.Ajuste
            ? await RedobrarComNovosDoLoteAsync(lote, chave, movimento, ct)
            : lote.EstadosJaAplicados.TryGetValue(chave, out var estadoAnteriorNoLote)
                ? DobraPosicao.AplicarIncremental(estadoAnteriorNoLote, movimento, movimento.DataEvento).Estado!
                : await AplicarNaPosicaoAsync(movimento.ClienteId, movimento.InstrumentoId, movimento, ct);

        if (resultado.IsFailure)
        {
            return resultado;
        }

        lote.EstadosJaAplicados[chave] = resultado.Value;

        if (!lote.MovimentosJaAdicionadosPorChave.TryGetValue(chave, out var movimentosDaChaveNoLote))
        {
            movimentosDaChaveNoLote = [];
            lote.MovimentosJaAdicionadosPorChave[chave] = movimentosDaChaveNoLote;
        }

        movimentosDaChaveNoLote.Add(movimento);

        return resultado;
    }

    private async Task<Result<PosicaoTresColunas>> RedobrarComNovosDoLoteAsync(
        LoteDeAplicacaoDePosicao lote,
        (string ClienteId, string InstrumentoId) chave,
        Movimento movimento,
        CancellationToken ct)
    {
        var movimentosDaChaveResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            chave.ClienteId, chave.InstrumentoId, ct);

        if (movimentosDaChaveResult.IsFailure)
        {
            return movimentosDaChaveResult.Error;
        }

        var jaAdicionadosNesteLote = lote.MovimentosJaAdicionadosPorChave.TryGetValue(chave, out var lista)
            ? lista
            : [];

        var todos = movimentosDaChaveResult.Value.Concat(jaAdicionadosNesteLote).Append(movimento).ToList();
        return DobraPosicao.Dobrar(todos);
    }

    private async Task<Result<PosicaoTresColunas>> AplicarNaPosicaoAsync(
        string clienteId, string instrumentoId, Movimento movimentoRecemCriado, CancellationToken ct)
    {
        var maxDataEventoResult = await movimentoReadRepository.ObterMaxDataEventoAsync(clienteId, instrumentoId, ct);

        if (maxDataEventoResult.IsFailure)
        {
            return maxDataEventoResult.Error;
        }

        var posicaoAtualResult = await posicaoCorrenteReadRepository.ObterAsync(clienteId, instrumentoId, ct);

        if (posicaoAtualResult.IsFailure)
        {
            return posicaoAtualResult.Error;
        }

        var resultadoIncremental = DobraPosicao.AplicarIncremental(
            posicaoAtualResult.Value, movimentoRecemCriado, maxDataEventoResult.Value.ComoNullable());

        if (!resultadoIncremental.ExigeRedobraDaChave)
        {
            return resultadoIncremental.Estado!;
        }

        var movimentosDaChaveResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
            clienteId, instrumentoId, ct);

        if (movimentosDaChaveResult.IsFailure)
        {
            return movimentosDaChaveResult.Error;
        }

        var todos = movimentosDaChaveResult.Value.Append(movimentoRecemCriado).ToList();
        return DobraPosicao.Dobrar(todos);
    }
}

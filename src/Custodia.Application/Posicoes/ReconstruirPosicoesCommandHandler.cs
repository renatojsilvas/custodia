using MediatR;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Posicoes;

public sealed class ReconstruirPosicoesCommandHandler(
    IMovimentoReadRepository movimentoReadRepository,
    IPosicaoCorrenteReadRepository posicaoCorrenteReadRepository,
    IPosicaoCorrenteWriteRepository posicaoCorrenteWriteRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ReconstruirPosicoesCommand, Result<ReconstruirPosicoesResultado>>
{
    public async Task<Result<ReconstruirPosicoesResultado>> Handle(
        ReconstruirPosicoesCommand request, CancellationToken ct)
    {
        var chavesNoLivroResult = await movimentoReadRepository.ObterChavesDistintasAsync(
            request.ClienteId, request.InstrumentoId, ct);

        if (chavesNoLivroResult.IsFailure)
        {
            return chavesNoLivroResult.Error;
        }

        var chavesNaProjecaoResult = await posicaoCorrenteReadRepository.ObterChavesAsync(
            request.ClienteId, request.InstrumentoId, ct);

        if (chavesNaProjecaoResult.IsFailure)
        {
            return chavesNaProjecaoResult.Error;
        }

        var chavesNoLivro = chavesNoLivroResult.Value;
        var chavesNoLivroConjunto = chavesNoLivro.ToHashSet();
        var chavesOrfas = chavesNaProjecaoResult.Value
            .Where(chave => !chavesNoLivroConjunto.Contains(chave))
            .ToList();

        var reconstruidas = 0;

        foreach (var chave in chavesNoLivro)
        {
            var movimentosDaChaveResult = await movimentoReadRepository.ObterMovimentosDaChaveAsync(
                chave.ClienteId, chave.InstrumentoId, ct);

            if (movimentosDaChaveResult.IsFailure)
            {
                return movimentosDaChaveResult.Error;
            }

            var estadoReconstruido = DobraPosicao.Dobrar(movimentosDaChaveResult.Value);

            var atualizarResult = await posicaoCorrenteWriteRepository.AtualizarAsync(
                chave.ClienteId, chave.InstrumentoId, estadoReconstruido, ct);

            if (atualizarResult.IsFailure)
            {
                return atualizarResult.Error;
            }

            reconstruidas++;
        }

        var removidas = 0;

        foreach (var chave in chavesOrfas)
        {
            var removerResult = await posicaoCorrenteWriteRepository.RemoverAsync(
                chave.ClienteId, chave.InstrumentoId, ct);

            if (removerResult.IsFailure)
            {
                return removerResult.Error;
            }

            removidas++;
        }

        var saveResult = await unitOfWork.SaveChangesAsync(ct);

        return saveResult.IsFailure
            ? saveResult.Error
            : new ReconstruirPosicoesResultado(reconstruidas, removidas);
    }
}

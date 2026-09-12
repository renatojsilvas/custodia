using Custodia.Application.Posicoes;
using Custodia.Application.Tests.Fakes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;

namespace Custodia.Application.Tests.Posicoes;

public sealed class ReconstruirPosicoesCommandHandlerTests
{
    private static readonly DateOnly Dia1 = new(2026, 8, 1);
    private static readonly DateOnly Dia2 = new(2026, 8, 2);
    private static readonly DateTimeOffset Agora = new(2026, 8, 15, 14, 0, 0, TimeSpan.Zero);

    private static ReconstruirPosicoesCommandHandler CriarHandler(
        IReadOnlyList<Movimento> movimentos,
        IReadOnlyDictionary<(string, string), PosicaoTresColunas>? posicoesExistentes,
        out FakePosicaoCorrenteWriteRepository posicaoWrite,
        out FakeUnitOfWork unitOfWork)
    {
        var movimentoRead = new FakeMovimentoReadRepository(movimentos);
        var posicaoRead = new FakePosicaoCorrenteReadRepository(posicoesExistentes);
        posicaoWrite = new FakePosicaoCorrenteWriteRepository();
        unitOfWork = new FakeUnitOfWork();

        return new ReconstruirPosicoesCommandHandler(movimentoRead, posicaoRead, posicaoWrite, unitOfWork);
    }

    [Fact]
    public async Task Handle_SemFiltro_ReconstroiTodasAsChavesDoLivroDobrandoNasTresColunas()
    {
        var compra = MovimentoTestFactory.Criar(
            1, "cli-a", "td:tesouro-a", TipoMovimento.Compra, Dia1, Agora, 10m, 1000m, "ref-1");
        var venda = MovimentoTestFactory.Criar(
            2, "cli-a", "td:tesouro-a", TipoMovimento.Venda, Dia2, Agora, -4m, 600m, "ref-2");
        var outraChave = MovimentoTestFactory.Criar(
            3, "cli-b", "td:tesouro-b", TipoMovimento.Compra, Dia1, Agora, 5m, 500m, "ref-3");

        var handler = CriarHandler(
            [compra, venda, outraChave],
            posicoesExistentes: null,
            out var posicaoWrite,
            out var unitOfWork);

        var resultado = await handler.Handle(new ReconstruirPosicoesCommand(null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, resultado.Value.ChavesReconstruidas);
        Assert.Equal(0, resultado.Value.ChavesRemovidasPorOrfandade);
        Assert.Equal(1, unitOfWork.ChamadasDeSaveChanges);

        var estadoA = posicaoWrite.UltimaPosicaoDe("cli-a", "td:tesouro-a");
        Assert.Equal(new PosicaoTresColunas(6m, 600m, 100m), estadoA);

        var estadoB = posicaoWrite.UltimaPosicaoDe("cli-b", "td:tesouro-b");
        Assert.Equal(new PosicaoTresColunas(5m, 500m, 100m), estadoB);
    }

    [Fact]
    public async Task Handle_ChaveExisteNaProjecaoENaoNoLivro_RemoveAChaveOrfaDaProjecao()
    {
        var compra = MovimentoTestFactory.Criar(
            1, "cli-a", "td:tesouro-a", TipoMovimento.Compra, Dia1, Agora, 10m, 1000m, "ref-1");

        var posicoesExistentes = new Dictionary<(string, string), PosicaoTresColunas>
        {
            [("cli-a", "td:tesouro-a")] = PosicaoTresColunas.Zero,
            [("cli-orfao", "td:tesouro-orfao")] = new PosicaoTresColunas(1m, 1m, 1m),
        };

        var handler = CriarHandler([compra], posicoesExistentes, out var posicaoWrite, out _);

        var resultado = await handler.Handle(new ReconstruirPosicoesCommand(null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.ChavesReconstruidas);
        Assert.Equal(1, resultado.Value.ChavesRemovidasPorOrfandade);
        Assert.Equal(new List<(string, string)> { ("cli-orfao", "td:tesouro-orfao") }, posicaoWrite.Removidas);
    }

    [Fact]
    public async Task Handle_ComFiltroDeClienteEInstrumento_RestringeAsChavesAlcancadas()
    {
        var doCliente = MovimentoTestFactory.Criar(
            1, "cli-a", "td:tesouro-a", TipoMovimento.Compra, Dia1, Agora, 10m, 1000m, "ref-1");
        var deOutroCliente = MovimentoTestFactory.Criar(
            2, "cli-b", "td:tesouro-b", TipoMovimento.Compra, Dia1, Agora, 5m, 500m, "ref-2");

        var movimentoRead = new FakeMovimentoReadRepository(
            [doCliente, deOutroCliente],
            chavesDistintas: (clienteId, instrumentoId) =>
                Result<IReadOnlyList<ChavePosicao>>.Success(
                    clienteId == "cli-a" ? [new ChavePosicao("cli-a", "td:tesouro-a")] : []));

        var posicaoRead = new FakePosicaoCorrenteReadRepository();
        var posicaoWrite = new FakePosicaoCorrenteWriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReconstruirPosicoesCommandHandler(movimentoRead, posicaoRead, posicaoWrite, unitOfWork);

        var resultado = await handler.Handle(
            new ReconstruirPosicoesCommand("cli-a", null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, resultado.Value.ChavesReconstruidas);
        var chaveAtualizada = Assert.Single(posicaoWrite.Atualizacoes);
        Assert.Equal("cli-a", chaveAtualizada.ClienteId);
        Assert.Equal("td:tesouro-a", chaveAtualizada.InstrumentoId);
    }

    [Fact]
    public async Task Handle_FalhaAoObterChavesDoLivro_PropagaOErroSemGravarNada()
    {
        var erro = new Error("Teste.Falha", "falha simulada", ErrorType.Unavailable);
        var movimentoRead = new FakeMovimentoReadRepository(
            [], chavesDistintas: (_, _) => Result<IReadOnlyList<ChavePosicao>>.Failure(erro));
        var posicaoRead = new FakePosicaoCorrenteReadRepository();
        var posicaoWrite = new FakePosicaoCorrenteWriteRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ReconstruirPosicoesCommandHandler(movimentoRead, posicaoRead, posicaoWrite, unitOfWork);

        var resultado = await handler.Handle(new ReconstruirPosicoesCommand(null, null), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(erro, resultado.Error);
        Assert.Empty(posicaoWrite.Atualizacoes);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Handle_SemChavesNoLivroENaProjecao_DevolveResultadoZeradoESalvaMesmoAssim()
    {
        var handler = CriarHandler([], posicoesExistentes: null, out var posicaoWrite, out var unitOfWork);

        var resultado = await handler.Handle(new ReconstruirPosicoesCommand(null, null), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, resultado.Value.ChavesReconstruidas);
        Assert.Equal(0, resultado.Value.ChavesRemovidasPorOrfandade);
        Assert.Empty(posicaoWrite.Atualizacoes);
        Assert.Equal(1, unitOfWork.ChamadasDeSaveChanges);
    }
}

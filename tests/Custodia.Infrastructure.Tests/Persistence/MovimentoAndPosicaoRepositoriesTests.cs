using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Infrastructure.Persistence.Repositories;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class MovimentoAndPosicaoRepositoriesTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateOnly DataPassada = new(2020, 1, 1);
    private static readonly DateTimeOffset Agora = DateTimeOffset.UtcNow;

    private static string NovoClienteId() => $"cli-repo-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:repo-{Guid.NewGuid():N}";

    private static string NovaRefExterna() => $"ref-repo-{Guid.NewGuid():N}";

    private NpgsqlDataSource CriarDataSource() => NpgsqlDataSource.Create(fixture.ConnectionString);

    private async Task<Movimento> InserirAsync(
        string clienteId, string instrumentoId, string refExterna, DateOnly? dataEvento = null,
        long? refEstorno = null, decimal qtdDelta = 10m, decimal valorFinanceiro = 1000m,
        TipoMovimento? tipo = null)
    {
        await using var db = fixture.CriarDbContext();
        var repo = new MovimentoWriteRepository(db);
        var unitOfWork = (IUnitOfWork)db;

        var movimento = Movimento.Create(
            clienteId, instrumentoId, tipo ?? TipoMovimento.Compra, dataEvento ?? DataPassada, Agora,
            qtdDelta, valorFinanceiro, refExterna, refEstorno).Value;

        await repo.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await unitOfWork.SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        return movimento;
    }

    [Fact]
    public async Task ObterPorClienteERefExternaAsync_Encontrado_DevolveMovimentoHidratadoComOsMesmosValores()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var refExterna = NovaRefExterna();
        var original = await InserirAsync(clienteId, instrumentoId, refExterna);

        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterPorClienteERefExternaAsync(clienteId, refExterna, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Encontrado);
        var lido = resultado.Value.Linha!;
        Assert.Equal(original.Id, lido.Id);
        Assert.Equal(clienteId, lido.ClienteId);
        Assert.Equal(instrumentoId, lido.InstrumentoId);
        Assert.Equal(TipoMovimento.Compra, lido.Tipo);
        Assert.Equal(original.DataEvento, lido.DataEvento);
        Assert.Equal(original.QtdDelta, lido.QtdDelta);
        Assert.Equal(original.ValorFinanceiro, lido.ValorFinanceiro);
        Assert.Equal(refExterna, lido.RefExterna);
        Assert.Null(lido.RefEstorno);
    }

    [Fact]
    public async Task ObterPorClienteERefExternaAsync_NaoEncontrado_DevolveNaoEncontrado()
    {
        var repo = new MovimentoReadRepository(CriarDataSource());

        var resultado = await repo.ObterPorClienteERefExternaAsync(
            NovoClienteId(), NovaRefExterna(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value.Encontrado);
        Assert.Null(resultado.Value.Linha);
    }

    [Fact]
    public async Task ObterPorRefExternaEmQualquerClienteAsync_ComMaisDeUmCliente_DevolveDeFormaDeterministicaOMenorId()
    {
        var refExterna = NovaRefExterna();
        var primeiro = await InserirAsync(NovoClienteId(), NovoInstrumentoId(), refExterna);
        var segundo = await InserirAsync(NovoClienteId(), NovoInstrumentoId(), refExterna);
        Assert.True(primeiro.Id < segundo.Id);

        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterPorRefExternaEmQualquerClienteAsync(refExterna, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Encontrado);
        Assert.Equal(primeiro.Id, resultado.Value.Linha!.Id);
        Assert.Equal(primeiro.ClienteId, resultado.Value.Linha!.ClienteId);
    }

    [Fact]
    public async Task ObterPorRefExternaEmQualquerClienteAsync_NaoEncontrado_DevolveNaoEncontrado()
    {
        var repo = new MovimentoReadRepository(CriarDataSource());

        var resultado = await repo.ObterPorRefExternaEmQualquerClienteAsync(NovaRefExterna(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value.Encontrado);
    }

    [Fact]
    public async Task ObterMaxDataEventoAsync_SemLinhasDaChave_DevolveInexistente()
    {
        var repo = new MovimentoReadRepository(CriarDataSource());

        var resultado = await repo.ObterMaxDataEventoAsync(NovoClienteId(), NovoInstrumentoId(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value.Existe);
        Assert.Null(resultado.Value.ComoNullable());
    }

    [Fact]
    public async Task ObterMaxDataEventoAsync_ComVariasLinhas_DevolveAMaiorDataEvento()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        await InserirAsync(clienteId, instrumentoId, NovaRefExterna(), dataEvento: new DateOnly(2024, 1, 10));
        await InserirAsync(clienteId, instrumentoId, NovaRefExterna(), dataEvento: new DateOnly(2024, 3, 5));
        await InserirAsync(clienteId, instrumentoId, NovaRefExterna(), dataEvento: new DateOnly(2024, 2, 1));

        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterMaxDataEventoAsync(clienteId, instrumentoId, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Existe);
        Assert.Equal(new DateOnly(2024, 3, 5), resultado.Value.Valor);
    }

    [Fact]
    public async Task ObterMovimentosDaChaveAsync_DevolveSomenteAsLinhasDaChaveInformada()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var m1 = await InserirAsync(clienteId, instrumentoId, NovaRefExterna());
        var m2 = await InserirAsync(clienteId, instrumentoId, NovaRefExterna());
        await InserirAsync(NovoClienteId(), instrumentoId, NovaRefExterna());
        await InserirAsync(clienteId, NovoInstrumentoId(), NovaRefExterna());

        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterMovimentosDaChaveAsync(clienteId, instrumentoId, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var ids = resultado.Value.Select(m => m.Id).ToHashSet();
        Assert.Equal(new HashSet<long> { m1.Id, m2.Id }, ids);
    }

    [Fact]
    public async Task PosicaoCorrenteReadRepository_ObterAsync_LinhaInexistente_DevolveZero()
    {
        var repo = new Custodia.Infrastructure.Persistence.Repositories.PosicaoCorrenteReadRepository(CriarDataSource());

        var resultado = await repo.ObterAsync(NovoClienteId(), NovoInstrumentoId(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(PosicaoTresColunas.Zero, resultado.Value);
    }

    [Fact]
    public async Task PosicaoCorrenteReadRepository_ObterAsync_ColunasNulasNoBanco_MapeiaParaZeroNoDominio()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                INSERT INTO posicao_corrente (cliente_id, instrumento_id, quantidade, preco_medio, custo_total)
                VALUES (@clienteId, @instrumentoId, 0, NULL, NULL)
                """,
                new { clienteId, instrumentoId });
        }

        var repo = new Custodia.Infrastructure.Persistence.Repositories.PosicaoCorrenteReadRepository(CriarDataSource());
        var resultado = await repo.ObterAsync(clienteId, instrumentoId, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0m, resultado.Value.Quantidade);
        Assert.Equal(0m, resultado.Value.PrecoMedio);
        Assert.Equal(0m, resultado.Value.CustoTotal);
    }

    [Fact]
    public async Task PosicaoCorrenteWriteRepository_AtualizarAsync_ChamadoDuasVezes_SobrescreveAsTresColunas()
    {
        await using var db = fixture.CriarDbContext();
        var writeRepo = new PosicaoCorrenteWriteRepository(db);
        var readRepo = new Custodia.Infrastructure.Persistence.Repositories.PosicaoCorrenteReadRepository(CriarDataSource());

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var primeiroEstado = new PosicaoTresColunas(10m, 1000m, 100m);
        await writeRepo.AtualizarAsync(clienteId, instrumentoId, primeiroEstado, CancellationToken.None);

        var segundoEstado = new PosicaoTresColunas(25m, 2500m, 100m);
        await writeRepo.AtualizarAsync(clienteId, instrumentoId, segundoEstado, CancellationToken.None);

        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        var resultado = await readRepo.ObterAsync(clienteId, instrumentoId, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(segundoEstado, resultado.Value);
    }

    [Fact]
    public async Task ObterChavesDistintasAsync_SemFiltro_DevolveUmaLinhaPorChaveDoLivro()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        await InserirAsync(clienteId, instrumentoId, NovaRefExterna());
        await InserirAsync(clienteId, instrumentoId, NovaRefExterna());
        var outroCliente = NovoClienteId();
        var outroInstrumento = NovoInstrumentoId();
        await InserirAsync(outroCliente, outroInstrumento, NovaRefExterna());

        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterChavesDistintasAsync(null, null, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Contains(new ChavePosicao(clienteId, instrumentoId), resultado.Value);
        Assert.Contains(new ChavePosicao(outroCliente, outroInstrumento), resultado.Value);
        Assert.Single(resultado.Value, c => c.ClienteId == clienteId && c.InstrumentoId == instrumentoId);
    }

    [Fact]
    public async Task ObterChavesDistintasAsync_ComFiltroDeClienteEInstrumento_RestringeAsChaves()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        await InserirAsync(clienteId, instrumentoId, NovaRefExterna());
        await InserirAsync(clienteId, NovoInstrumentoId(), NovaRefExterna());
        await InserirAsync(NovoClienteId(), instrumentoId, NovaRefExterna());

        var repo = new MovimentoReadRepository(CriarDataSource());
        var resultado = await repo.ObterChavesDistintasAsync(clienteId, instrumentoId, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var chave = Assert.Single(resultado.Value);
        Assert.Equal(clienteId, chave.ClienteId);
        Assert.Equal(instrumentoId, chave.InstrumentoId);
    }

    [Fact]
    public async Task PosicaoCorrenteReadRepository_ObterChavesAsync_DevolveAsChavesGravadas()
    {
        await using var db = fixture.CriarDbContext();
        var writeRepo = new PosicaoCorrenteWriteRepository(db);

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        await writeRepo.AtualizarAsync(clienteId, instrumentoId, new PosicaoTresColunas(1m, 1m, 1m), CancellationToken.None);
        await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);

        var readRepo = new Custodia.Infrastructure.Persistence.Repositories.PosicaoCorrenteReadRepository(CriarDataSource());
        var resultado = await readRepo.ObterChavesAsync(clienteId, null, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var chave = Assert.Single(resultado.Value);
        Assert.Equal(clienteId, chave.ClienteId);
        Assert.Equal(instrumentoId, chave.InstrumentoId);
    }

    [Fact]
    public async Task PosicaoCorrenteWriteRepository_RemoverAsync_RemoveALinhaEDeixaAsDemaisIntactas()
    {
        await using var db = fixture.CriarDbContext();
        var writeRepo = new PosicaoCorrenteWriteRepository(db);

        var clienteId = NovoClienteId();
        var instrumentoParaRemover = NovoInstrumentoId();
        var instrumentoParaManter = NovoInstrumentoId();

        await writeRepo.AtualizarAsync(
            clienteId, instrumentoParaRemover, new PosicaoTresColunas(1m, 1m, 1m), CancellationToken.None);
        await writeRepo.AtualizarAsync(
            clienteId, instrumentoParaManter, new PosicaoTresColunas(2m, 2m, 2m), CancellationToken.None);
        await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);

        await writeRepo.RemoverAsync(clienteId, instrumentoParaRemover, CancellationToken.None);
        var salvou = await ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        var readRepo = new Custodia.Infrastructure.Persistence.Repositories.PosicaoCorrenteReadRepository(CriarDataSource());
        var chavesRestantes = await readRepo.ObterChavesAsync(clienteId, null, CancellationToken.None);

        Assert.True(chavesRestantes.IsSuccess);
        var chave = Assert.Single(chavesRestantes.Value);
        Assert.Equal(instrumentoParaManter, chave.InstrumentoId);

        var estadoRemovido = await readRepo.ObterAsync(clienteId, instrumentoParaRemover, CancellationToken.None);
        Assert.Equal(PosicaoTresColunas.Zero, estadoRemovido.Value);
    }
}

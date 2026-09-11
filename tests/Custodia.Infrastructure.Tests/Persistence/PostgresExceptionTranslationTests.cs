using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class PostgresExceptionTranslationTests(InfrastructurePostgresFixture fixture)
{
    private static readonly DateOnly DataPassada = new(2020, 1, 1);
    private static readonly DateTimeOffset Agora = DateTimeOffset.UtcNow;

    private static string NovoClienteId() => $"cli-tx-{Guid.NewGuid():N}";

    private static string NovoInstrumentoId() => $"td:tx-{Guid.NewGuid():N}";

    private static string NovaRefExterna() => $"ref-tx-{Guid.NewGuid():N}";

    private static Movimento MovimentoValido(
        string clienteId, string instrumentoId, string refExterna, long? refEstorno = null,
        decimal qtdDelta = 10m, decimal valorFinanceiro = 1000m, DateOnly? dataEvento = null,
        TipoMovimento? tipo = null) =>
        Movimento.Create(
            clienteId, instrumentoId, tipo ?? TipoMovimento.Compra, dataEvento ?? DataPassada, Agora,
            qtdDelta, valorFinanceiro, refExterna, refEstorno).Value;

    [Fact]
    public async Task SaveChanges_SegundoAjusteApontandoParaOMesmoOriginal_TraduzParaRefEstornoDuplicado()
    {
        await using var db = fixture.CriarDbContext();
        var unitOfWork = (IUnitOfWork)db;
        var movimentoWrite = new MovimentoWriteRepository(db);

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        var original = MovimentoValido(clienteId, instrumentoId, NovaRefExterna());
        await movimentoWrite.AdicionarAsync(original, CancellationToken.None);
        var salvouOriginal = await unitOfWork.SaveChangesAsync(CancellationToken.None);
        Assert.True(salvouOriginal.IsSuccess);

        var ajuste1 = MovimentoValido(
            clienteId, instrumentoId, NovaRefExterna(), refEstorno: original.Id,
            qtdDelta: -10m, valorFinanceiro: -1000m, tipo: TipoMovimento.Ajuste);
        await movimentoWrite.AdicionarAsync(ajuste1, CancellationToken.None);
        var salvouAjuste1 = await unitOfWork.SaveChangesAsync(CancellationToken.None);
        Assert.True(salvouAjuste1.IsSuccess);

        var ajuste2 = MovimentoValido(
            clienteId, instrumentoId, NovaRefExterna(), refEstorno: original.Id,
            qtdDelta: -10m, valorFinanceiro: -1000m, tipo: TipoMovimento.Ajuste);
        await movimentoWrite.AdicionarAsync(ajuste2, CancellationToken.None);
        var resultado = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoWriteErrors.RefEstornoDuplicado, resultado.Error);
    }

    [Theory]
    [InlineData("cliente")]
    [InlineData("instrumento")]
    [InlineData("refExterna")]
    public async Task SaveChanges_IdentificadorComEspacoNaBorda_TraduzParaErroUnico(string qual)
    {
        await using var db = fixture.CriarDbContext();
        var unitOfWork = (IUnitOfWork)db;
        var movimentoWrite = new MovimentoWriteRepository(db);

        var clienteId = qual == "cliente" ? $" {NovoClienteId()}" : NovoClienteId();
        var instrumentoId = qual == "instrumento" ? $" {NovoInstrumentoId()}" : NovoInstrumentoId();
        var refExterna = qual == "refExterna" ? $" {NovaRefExterna()}" : NovaRefExterna();

        var movimento = MovimentoValido(clienteId, instrumentoId, refExterna);
        await movimentoWrite.AdicionarAsync(movimento, CancellationToken.None);
        var resultado = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoWriteErrors.IdentificadorComEspacoNaBorda, resultado.Error);
    }

    [Fact]
    public async Task SaveChanges_ReentregaConcorrenteComMesmoClienteERefExterna_TraduzParaMensagemDuplicada()
    {
        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var refExterna = NovaRefExterna();

        await using (var db1 = fixture.CriarDbContext())
        {
            var unitOfWork1 = (IUnitOfWork)db1;
            var movimentoWrite1 = new MovimentoWriteRepository(db1);
            await movimentoWrite1.AdicionarAsync(MovimentoValido(clienteId, instrumentoId, refExterna), CancellationToken.None);
            var salvou = await unitOfWork1.SaveChangesAsync(CancellationToken.None);
            Assert.True(salvou.IsSuccess);
        }

        await using var db2 = fixture.CriarDbContext();
        var unitOfWork2 = (IUnitOfWork)db2;
        var movimentoWrite2 = new MovimentoWriteRepository(db2);
        await movimentoWrite2.AdicionarAsync(MovimentoValido(clienteId, instrumentoId, refExterna), CancellationToken.None);
        var resultado = await unitOfWork2.SaveChangesAsync(CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoWriteErrors.MensagemDuplicada, resultado.Error);
    }

    [Fact]
    public async Task AtualizarPosicao_QuantidadeComOnzeDigitosInteiros_TraduzParaValorNumericoExcedeMagnitudeOuEscalaSuportada()
    {
        await using var db = fixture.CriarDbContext();
        var posicaoWrite = new PosicaoCorrenteWriteRepository(db);

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var estado = new PosicaoTresColunas(12345678901.12345678m, 100m, 10m);

        var resultado = await posicaoWrite.AtualizarAsync(clienteId, instrumentoId, estado, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada, resultado.Error);
    }

    [Fact]
    public async Task SaveChanges_DataEventoFutura_TraduzParaDataEventoFutura()
    {
        await using var db = fixture.CriarDbContext();
        var unitOfWork = (IUnitOfWork)db;
        var movimentoWrite = new MovimentoWriteRepository(db);

        var dataFutura = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        var movimento = MovimentoValido(NovoClienteId(), NovoInstrumentoId(), NovaRefExterna(), dataEvento: dataFutura);
        await movimentoWrite.AdicionarAsync(movimento, CancellationToken.None);
        var resultado = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoWriteErrors.DataEventoFutura, resultado.Error);
    }

    [Fact]
    public async Task SaveChanges_MarcarMovimentoExistenteComoModificado_TraduzParaOperacaoNaoPermitidaSobreMovimentoImutavel()
    {
        await using var db = fixture.CriarDbContext();
        var unitOfWork = (IUnitOfWork)db;
        var movimentoWrite = new MovimentoWriteRepository(db);

        var movimento = MovimentoValido(NovoClienteId(), NovoInstrumentoId(), NovaRefExterna());
        await movimentoWrite.AdicionarAsync(movimento, CancellationToken.None);
        var salvou = await unitOfWork.SaveChangesAsync(CancellationToken.None);
        Assert.True(salvou.IsSuccess);

        db.Entry(movimento).State = EntityState.Modified;
        var resultado = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel, resultado.Error);
    }

    [Fact]
    public async Task SaveChanges_SegundoMovimentoDoLoteViolaConstraint_NaoDeixaNadaGravadoDoPrimeiro()
    {
        await using var db = fixture.CriarDbContext();
        var unitOfWork = (IUnitOfWork)db;
        var movimentoWrite = new MovimentoWriteRepository(db);

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();
        var refExternaRepetida = NovaRefExterna();

        await movimentoWrite.AdicionarAsync(
            MovimentoValido(clienteId, instrumentoId, refExternaRepetida), CancellationToken.None);
        await movimentoWrite.AdicionarAsync(
            MovimentoValido(clienteId, InstrumentosCaixa.Brl, refExternaRepetida, qtdDelta: -1000m, valorFinanceiro: 1000m),
            CancellationToken.None);

        var resultado = await unitOfWork.SaveChangesAsync(CancellationToken.None);
        Assert.True(resultado.IsFailure);

        await using var verificacao = fixture.CriarDbContext();
        var quantidadeGravada = await verificacao.Movimentos.CountAsync(m => m.ClienteId == clienteId);
        Assert.Equal(0, quantidadeGravada);
    }

    [Fact]
    public async Task GravacaoDeMovimentoEAtualizacaoDePosicao_ParticipamDaMesmaTransacao()
    {
        await using var db = fixture.CriarDbContext();
        var movimentoWrite = new MovimentoWriteRepository(db);
        var posicaoWrite = new PosicaoCorrenteWriteRepository(db);

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await movimentoWrite.AdicionarAsync(
            MovimentoValido(clienteId, instrumentoId, NovaRefExterna()), CancellationToken.None);
        var transacaoAposMovimento = db.Database.CurrentTransaction;
        Assert.NotNull(transacaoAposMovimento);

        await posicaoWrite.AtualizarAsync(
            clienteId, instrumentoId, new PosicaoTresColunas(10m, 1000m, 100m), CancellationToken.None);
        var transacaoAposPosicao = db.Database.CurrentTransaction;

        Assert.Same(transacaoAposMovimento, transacaoAposPosicao);
    }

    [Fact]
    public async Task AtualizarPosicao_ComFalha_DesfazTransacaoENaoDeixaOMovimentoJaAdicionadoSerGravado()
    {
        await using var db = fixture.CriarDbContext();
        var movimentoWrite = new MovimentoWriteRepository(db);
        var posicaoWrite = new PosicaoCorrenteWriteRepository(db);
        var unitOfWork = (IUnitOfWork)db;

        var clienteId = NovoClienteId();
        var instrumentoId = NovoInstrumentoId();

        await movimentoWrite.AdicionarAsync(
            MovimentoValido(clienteId, instrumentoId, NovaRefExterna()), CancellationToken.None);

        var resultadoPosicao = await posicaoWrite.AtualizarAsync(
            clienteId, instrumentoId, new PosicaoTresColunas(12345678901.12345678m, 100m, 10m), CancellationToken.None);
        Assert.True(resultadoPosicao.IsFailure);

        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries());

        var salvouDepoisDaFalha = await unitOfWork.SaveChangesAsync(CancellationToken.None);
        Assert.True(salvouDepoisDaFalha.IsSuccess);

        await using var verificacao = fixture.CriarDbContext();
        var quantidadeGravada = await verificacao.Movimentos.CountAsync(m => m.ClienteId == clienteId);
        Assert.Equal(0, quantidadeGravada);
    }

    [Fact]
    public void LimparRastreamento_RemoveEntidadesRastreadas()
    {
        using var db = fixture.CriarDbContext();
        var unitOfWork = (IUnitOfWork)db;

        db.Movimentos.Add(MovimentoValido(NovoClienteId(), NovoInstrumentoId(), NovaRefExterna()));
        Assert.NotEmpty(db.ChangeTracker.Entries());

        unitOfWork.LimparRastreamento();

        Assert.Empty(db.ChangeTracker.Entries());
    }
}

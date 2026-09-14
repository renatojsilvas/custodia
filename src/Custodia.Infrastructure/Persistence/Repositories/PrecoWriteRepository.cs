using Custodia.Application.Precos;
using Custodia.Domain.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class PrecoWriteRepository(AppDbContext dbContext) : IPrecoWriteRepository
{
    static PrecoWriteRepository()
    {
        DapperTypeHandlers.Register();
    }

    private sealed record LinhaCampoAnterior(string? CampoAnterior);

    public async Task<Result<ResultadoHistorico>> RegistrarHistoricoAsync(ObservacaoDePreco observacao, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        try
        {
            var linhasAfetadas = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO historico_precos (instrumento_id, data_ref, campo, fonte, valor, revisao, observado_em)
                VALUES (
                    {observacao.InstrumentoId}, {observacao.DataRef}, {observacao.Campo}, {observacao.Fonte},
                    {observacao.Valor}, {observacao.Revisao}, {observacao.ObservadoEm})
                ON CONFLICT (instrumento_id, data_ref, campo, fonte, revisao) DO NOTHING
                """,
                ct);

            if (linhasAfetadas > 0)
            {
                return Result<ResultadoHistorico>.Success(ResultadoHistorico.Inserido());
            }

            var valorExistente = await ObterValorHistoricoAsync(observacao, ct);

            return valorExistente == observacao.Valor
                ? Result<ResultadoHistorico>.Success(ResultadoHistorico.ReplayInocuo())
                : Result<ResultadoHistorico>.Success(ResultadoHistorico.Divergente(valorExistente));
        }
        catch (PostgresException pg)
        {
            await dbContext.DesfazerTransacaoAmbienteAsync(ct);

            var erro = PostgresExceptionTranslator.Traduzir(pg);
            if (erro is null)
            {
                throw;
            }

            return Result<ResultadoHistorico>.Failure(erro);
        }
    }

    public async Task<Result<ResultadoAtualizacaoPrecoAtual>> AtualizarPrecoAtualAsync(
        ObservacaoDePreco observacao, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        try
        {
            var linhasAfetadas = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE preco_atual
                SET data_ref = {observacao.DataRef}, valor = {observacao.Valor}, revisao = {observacao.Revisao}
                WHERE instrumento_id = {observacao.InstrumentoId}
                  AND campo = {observacao.Campo}
                  AND (data_ref, revisao) <= ({observacao.DataRef}, {observacao.Revisao})
                """,
                ct);

            if (linhasAfetadas > 0)
            {
                return Result<ResultadoAtualizacaoPrecoAtual>.Success(ResultadoAtualizacaoPrecoAtual.Atualizado());
            }

            var motivo = await DiagnosticarNaoAtualizacaoAsync(observacao, ct);

            return Result<ResultadoAtualizacaoPrecoAtual>.Success(ResultadoAtualizacaoPrecoAtual.NaoAtualizado(motivo));
        }
        catch (PostgresException pg)
        {
            await dbContext.DesfazerTransacaoAmbienteAsync(ct);

            var erro = PostgresExceptionTranslator.Traduzir(pg);
            if (erro is null)
            {
                throw;
            }

            return Result<ResultadoAtualizacaoPrecoAtual>.Failure(erro);
        }
    }

    public async Task<Result<ValorRevisaoAnteriorConsulta>> ObterValorRevisaoAnteriorAsync(
        ObservacaoDePreco observacao, CancellationToken ct)
    {
        var transacao = await dbContext.ObterOuAbrirTransacaoAsync(ct);
        var conexao = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        var valor = await conexao.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(
            """
            SELECT valor FROM historico_precos
            WHERE instrumento_id = @InstrumentoId AND data_ref = @DataRef AND campo = @Campo
              AND fonte = @Fonte AND revisao = @RevisaoAnterior
            """,
            new
            {
                observacao.InstrumentoId,
                observacao.DataRef,
                observacao.Campo,
                observacao.Fonte,
                RevisaoAnterior = observacao.Revisao - 1,
            },
            transacao.GetDbTransaction(),
            cancellationToken: ct));

        return Result<ValorRevisaoAnteriorConsulta>.Success(
            valor.HasValue ? ValorRevisaoAnteriorConsulta.De(valor.Value) : ValorRevisaoAnteriorConsulta.Inexistente);
    }

    public async Task<Result<ResultadoRegistroBootstrap>> RegistrarBootstrapAsync(
        ObservacaoDePreco observacao, CancellationToken ct)
    {
        var historicoResult = await RegistrarHistoricoAsync(observacao, ct);

        if (historicoResult.IsFailure)
        {
            return historicoResult.Error;
        }

        if (historicoResult.Value.Tipo == ResultadoHistoricoTipo.Divergente)
        {
            return Result<ResultadoRegistroBootstrap>.Success(new ResultadoRegistroBootstrap(historicoResult.Value, null));
        }

        try
        {
            var transacao = await dbContext.ObterOuAbrirTransacaoAsync(ct);
            var conexao = (NpgsqlConnection)dbContext.Database.GetDbConnection();

            var linha = await conexao.QuerySingleOrDefaultAsync<LinhaCampoAnterior>(new CommandDefinition(
                """
                WITH antes AS (
                    SELECT campo FROM preco_atual WHERE instrumento_id = @InstrumentoId
                )
                INSERT INTO preco_atual (instrumento_id, data_ref, campo, valor, revisao)
                VALUES (@InstrumentoId, @DataRef, @Campo, @Valor, @Revisao)
                ON CONFLICT (instrumento_id) DO UPDATE SET
                    data_ref = EXCLUDED.data_ref,
                    campo = EXCLUDED.campo,
                    valor = EXCLUDED.valor,
                    revisao = EXCLUDED.revisao
                WHERE preco_atual.campo <> EXCLUDED.campo
                   OR (EXCLUDED.data_ref, EXCLUDED.revisao) >= (preco_atual.data_ref, preco_atual.revisao)
                RETURNING (SELECT campo FROM antes) AS "CampoAnterior"
                """,
                observacao,
                transacao.GetDbTransaction(),
                cancellationToken: ct));

            var precoAtual = ClassificarResultadoDeBootstrap(linha, observacao.Campo);

            return Result<ResultadoRegistroBootstrap>.Success(
                new ResultadoRegistroBootstrap(historicoResult.Value, precoAtual));
        }
        catch (PostgresException pg)
        {
            await dbContext.DesfazerTransacaoAmbienteAsync(ct);

            var erro = PostgresExceptionTranslator.Traduzir(pg);
            if (erro is null)
            {
                throw;
            }

            return Result<ResultadoRegistroBootstrap>.Failure(erro);
        }
    }

    private static ResultadoBootstrapPrecoAtual ClassificarResultadoDeBootstrap(LinhaCampoAnterior? linha, string campoNovo)
    {
        if (linha is null)
        {
            return ResultadoBootstrapPrecoAtual.IgnoradoMaisAntigo();
        }

        if (linha.CampoAnterior is null)
        {
            return ResultadoBootstrapPrecoAtual.Criado();
        }

        return linha.CampoAnterior == campoNovo
            ? ResultadoBootstrapPrecoAtual.AtualizadoMesmoCampo(linha.CampoAnterior)
            : ResultadoBootstrapPrecoAtual.CampoTrocado(linha.CampoAnterior);
    }

    private async Task<decimal> ObterValorHistoricoAsync(ObservacaoDePreco observacao, CancellationToken ct)
    {
        var transacao = await dbContext.ObterOuAbrirTransacaoAsync(ct);
        var conexao = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        return await conexao.QuerySingleAsync<decimal>(new CommandDefinition(
            """
            SELECT valor FROM historico_precos
            WHERE instrumento_id = @InstrumentoId AND data_ref = @DataRef AND campo = @Campo
              AND fonte = @Fonte AND revisao = @Revisao
            """,
            observacao,
            transacao.GetDbTransaction(),
            cancellationToken: ct));
    }

    private async Task<MotivoPrecoAtualNaoAtualizado> DiagnosticarNaoAtualizacaoAsync(
        ObservacaoDePreco observacao, CancellationToken ct)
    {
        var transacao = await dbContext.ObterOuAbrirTransacaoAsync(ct);
        var conexao = (NpgsqlConnection)dbContext.Database.GetDbConnection();

        var linha = await conexao.QuerySingleOrDefaultAsync<LinhaDiagnostico>(new CommandDefinition(
            """
            SELECT campo AS "Campo", data_ref AS "DataRef", revisao AS "Revisao"
            FROM preco_atual
            WHERE instrumento_id = @InstrumentoId
            """,
            new { observacao.InstrumentoId },
            transacao.GetDbTransaction(),
            cancellationToken: ct));

        if (linha is null)
        {
            return MotivoPrecoAtualNaoAtualizado.SemLinha;
        }

        return linha.Campo != observacao.Campo
            ? MotivoPrecoAtualNaoAtualizado.CampoDiferenteDoGravado
            : MotivoPrecoAtualNaoAtualizado.MaisVelhoQueArmazenado;
    }

    private sealed record LinhaDiagnostico(string Campo, DateOnly DataRef, int Revisao);
}

using Custodia.Application.Precos;
using Custodia.Domain.Common;
using Custodia.Domain.Precos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Custodia.Infrastructure.Persistence.Repositories;

public sealed class PrecoWriteRepository(AppDbContext dbContext) : IPrecoWriteRepository
{
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
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        var linhas = await dbContext.HistoricoPrecos
            .FromSqlInterpolated(
                $"""
                SELECT * FROM historico_precos
                WHERE instrumento_id = {observacao.InstrumentoId} AND data_ref = {observacao.DataRef} AND campo = {observacao.Campo}
                  AND fonte = {observacao.Fonte} AND revisao = {observacao.Revisao - 1}
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        var linha = linhas.SingleOrDefault();

        return Result<ValorRevisaoAnteriorConsulta>.Success(
            linha is null ? ValorRevisaoAnteriorConsulta.Inexistente : ValorRevisaoAnteriorConsulta.De(linha.Valor));
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
            await dbContext.ObterOuAbrirTransacaoAsync(ct);

            var linhas = await dbContext.Database
                .SqlQuery<string?>(
                    $"""
                    WITH antes AS (
                        SELECT campo FROM preco_atual WHERE instrumento_id = {observacao.InstrumentoId}
                    )
                    INSERT INTO preco_atual (instrumento_id, data_ref, campo, valor, revisao)
                    VALUES ({observacao.InstrumentoId}, {observacao.DataRef}, {observacao.Campo}, {observacao.Valor}, {observacao.Revisao})
                    ON CONFLICT (instrumento_id) DO UPDATE SET
                        data_ref = EXCLUDED.data_ref,
                        campo = EXCLUDED.campo,
                        valor = EXCLUDED.valor,
                        revisao = EXCLUDED.revisao
                    WHERE preco_atual.campo <> EXCLUDED.campo
                       OR (EXCLUDED.data_ref, EXCLUDED.revisao) >= (preco_atual.data_ref, preco_atual.revisao)
                    RETURNING (SELECT campo FROM antes) AS "CampoAnterior"
                    """)
                .ToListAsync(ct);

            var precoAtual = ClassificarResultadoDeBootstrap(linhas, observacao.Campo);

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

    private static ResultadoBootstrapPrecoAtual ClassificarResultadoDeBootstrap(IReadOnlyList<string?> linhas, string campoNovo)
    {
        if (linhas.Count == 0)
        {
            return ResultadoBootstrapPrecoAtual.IgnoradoMaisAntigo();
        }

        var campoAnterior = linhas.Single();

        if (campoAnterior is null)
        {
            return ResultadoBootstrapPrecoAtual.Criado();
        }

        return campoAnterior == campoNovo
            ? ResultadoBootstrapPrecoAtual.AtualizadoMesmoCampo(campoAnterior)
            : ResultadoBootstrapPrecoAtual.CampoTrocado(campoAnterior);
    }

    private async Task<decimal> ObterValorHistoricoAsync(ObservacaoDePreco observacao, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        var linhas = await dbContext.HistoricoPrecos
            .FromSqlInterpolated(
                $"""
                SELECT * FROM historico_precos
                WHERE instrumento_id = {observacao.InstrumentoId} AND data_ref = {observacao.DataRef} AND campo = {observacao.Campo}
                  AND fonte = {observacao.Fonte} AND revisao = {observacao.Revisao}
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas.Single().Valor;
    }

    private async Task<MotivoPrecoAtualNaoAtualizado> DiagnosticarNaoAtualizacaoAsync(
        ObservacaoDePreco observacao, CancellationToken ct)
    {
        await dbContext.ObterOuAbrirTransacaoAsync(ct);

        var linhas = await dbContext.PrecosAtuais
            .FromSqlInterpolated($"SELECT * FROM preco_atual WHERE instrumento_id = {observacao.InstrumentoId}")
            .AsNoTracking()
            .ToListAsync(ct);

        var linha = linhas.SingleOrDefault();

        if (linha is null)
        {
            return MotivoPrecoAtualNaoAtualizado.SemLinha;
        }

        return linha.Campo != observacao.Campo
            ? MotivoPrecoAtualNaoAtualizado.CampoDiferenteDoGravado
            : MotivoPrecoAtualNaoAtualizado.MaisVelhoQueArmazenado;
    }
}

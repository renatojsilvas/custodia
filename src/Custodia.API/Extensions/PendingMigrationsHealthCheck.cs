using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Custodia.Infrastructure.Persistence;

namespace Custodia.API.Extensions;

public sealed class PendingMigrationsHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pendentes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pendentes.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"{pendentes.Count} migration(ns) pendente(s): {string.Join(", ", pendentes)}.");
            }

            var tabelasAusentes = await TabelasAusentesAsync(cancellationToken);

            if (tabelasAusentes.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Tabela(s) do modelo ausente(s) no schema físico (drift manual): " +
                    $"{string.Join(", ", tabelasAusentes)}.");
            }

            var triggersAusentes = await TriggersAusentesAsync(cancellationToken);

            if (triggersAusentes.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Trigger(s) do modelo ausente(s) no schema físico (drift manual): " +
                    $"{string.Join(", ", triggersAusentes)}. A guarda de imutabilidade da tabela pode " +
                    "ter sido removida por fora, com UPDATE/DELETE deixando de ser bloqueados.");
            }

            var chavesAusentes = await ChavesAusentesAsync(cancellationToken);

            if (chavesAusentes.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Chave(s) primária(s) ou alternada(s) do modelo ausente(s) no schema físico " +
                    $"(drift manual): {string.Join(", ", chavesAusentes)}.");
            }

            var fksAusentes = await ForeignKeysAusentesAsync(cancellationToken);

            if (fksAusentes.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Foreign key(s) do modelo ausente(s) no schema físico (drift manual): " +
                    $"{string.Join(", ", fksAusentes)}.");
            }

            var indicesAusentes = await IndicesAusentesAsync(cancellationToken);

            if (indicesAusentes.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Índice(s) do modelo ausente(s) no schema físico (drift manual): " +
                    $"{string.Join(", ", indicesAusentes)}.");
            }

            return HealthCheckResult.Healthy(
                "Nenhuma migration pendente; tabelas, triggers, chaves, foreign keys e " +
                "índices do modelo presentes no schema.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Falha ao verificar migrations pendentes e schema.", ex);
        }
    }

    private async Task<IReadOnlyList<string>> TabelasAusentesAsync(CancellationToken cancellationToken)
    {
        var tabelas = db.Model.GetEntityTypes()
            .Select(entidade => entidade.GetTableName())
            .Where(nome => nome is not null)
            .Select(nome => nome!)
            .Distinct()
            .ToArray();

        if (tabelas.Length == 0)
        {
            return [];
        }

        return await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT t.nome
                FROM unnest({0}) AS t(nome)
                WHERE to_regclass('public.' || t.nome) IS NULL
                """,
                new object[] { tabelas })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> TriggersAusentesAsync(CancellationToken cancellationToken)
    {
        var triggersEsperados = db.Model.GetEntityTypes()
            .SelectMany(entidade => entidade.GetDeclaredTriggers())
            .Select(trigger => $"{trigger.GetTableName()}:{trigger.GetDatabaseName()}")
            .Distinct()
            .ToArray();

        if (triggersEsperados.Length == 0)
        {
            return [];
        }

        var triggersExistentes = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT c.relname || ':' || trg.tgname AS nome
                FROM pg_trigger trg
                JOIN pg_class c ON c.oid = trg.tgrelid
                WHERE trg.tgisinternal = false
                """)
            .ToListAsync(cancellationToken);

        return triggersEsperados.Except(triggersExistentes).ToList();
    }

    private async Task<IReadOnlyList<string>> ChavesAusentesAsync(CancellationToken cancellationToken)
    {
        var chavesEsperadas = db.Model.GetEntityTypes()
            .SelectMany(entidade => entidade.GetKeys())
            .Select(chave => chave.GetName())
            .Where(nome => nome is not null)
            .Select(nome => nome!)
            .Distinct()
            .ToArray();

        if (chavesEsperadas.Length == 0)
        {
            return [];
        }

        var chavesExistentes = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT conname AS nome
                FROM pg_constraint
                WHERE contype IN ('p', 'u')
                """)
            .ToListAsync(cancellationToken);

        return chavesEsperadas.Except(chavesExistentes).ToList();
    }

    private async Task<IReadOnlyList<string>> ForeignKeysAusentesAsync(CancellationToken cancellationToken)
    {
        var fksEsperadas = db.Model.GetEntityTypes()
            .SelectMany(entidade => entidade.GetForeignKeys())
            .Select(fk => fk.GetConstraintName())
            .Where(nome => nome is not null)
            .Select(nome => nome!)
            .Distinct()
            .ToArray();

        if (fksEsperadas.Length == 0)
        {
            return [];
        }

        var fksExistentes = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT conname AS nome
                FROM pg_constraint
                WHERE contype = 'f'
                """)
            .ToListAsync(cancellationToken);

        return fksEsperadas.Except(fksExistentes).ToList();
    }

    private async Task<IReadOnlyList<string>> IndicesAusentesAsync(CancellationToken cancellationToken)
    {
        var indicesEsperados = db.Model.GetEntityTypes()
            .SelectMany(entidade => entidade.GetIndexes())
            .Select(indice => indice.GetDatabaseName())
            .Where(nome => nome is not null)
            .Select(nome => nome!)
            .Distinct()
            .ToArray();

        if (indicesEsperados.Length == 0)
        {
            return [];
        }

        var indicesExistentes = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT indexname AS nome
                FROM pg_indexes
                WHERE schemaname = 'public'
                """)
            .ToListAsync(cancellationToken);

        return indicesEsperados.Except(indicesExistentes).ToList();
    }
}

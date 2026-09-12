using Custodia.Application.Common.Interfaces;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Precos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Custodia.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Movimento> Movimentos => Set<Movimento>();

    public DbSet<PosicaoCorrente> PosicoesCorrentes => Set<PosicaoCorrente>();

    public DbSet<PrecoAtual> PrecosAtuais => Set<PrecoAtual>();

    public DbSet<HistoricoPreco> HistoricoPrecos => Set<HistoricoPreco>();

    public DbSet<SnapshotPosicao> SnapshotsPosicao => Set<SnapshotPosicao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    internal async Task<IDbContextTransaction> ObterOuAbrirTransacaoAsync(CancellationToken ct) =>
        Database.CurrentTransaction ?? await Database.BeginTransactionAsync(ct);

    internal async Task DesfazerTransacaoAmbienteAsync(CancellationToken ct)
    {
        var transacao = Database.CurrentTransaction;
        if (transacao is not null)
        {
            await transacao.RollbackAsync(ct);
            await transacao.DisposeAsync();
        }

        DetacharAlteracoes();
    }

    async Task<Result> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        var transacaoAmbiente = Database.CurrentTransaction;

        try
        {
            await base.SaveChangesAsync(cancellationToken);

            if (transacaoAmbiente is not null)
            {
                await transacaoAmbiente.CommitAsync(cancellationToken);
                await transacaoAmbiente.DisposeAsync();
            }

            return Result.Success();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException)
        {
            var pg = (PostgresException)ex.InnerException!;
            await DesfazerTransacaoAmbienteAsync(cancellationToken);

            var erro = PostgresExceptionTranslator.Traduzir(pg);
            if (erro is null)
            {
                throw;
            }

            return Result.Failure(erro);
        }
    }

    private void DetacharAlteracoes()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    void IUnitOfWork.LimparRastreamento()
    {
        ChangeTracker.Clear();
    }
}

using Custodia.Domain.Movimentos;
using Custodia.Domain.Posicoes;
using Custodia.Domain.Precos;
using Microsoft.EntityFrameworkCore;

namespace Custodia.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
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
}

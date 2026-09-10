using Custodia.Domain.Common;
using Custodia.Domain.Precos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Custodia.Infrastructure.Persistence.Configurations;

public sealed class HistoricoPrecoConfiguration : IEntityTypeConfiguration<HistoricoPreco>
{
    public void Configure(EntityTypeBuilder<HistoricoPreco> builder)
    {
        builder.ToTable("historico_precos");

        builder.HasKey(h => new { h.InstrumentoId, h.DataRef, h.Campo, h.Fonte, h.Revisao });

        builder.Property(h => h.InstrumentoId)
            .HasColumnName("instrumento_id")
            .IsRequired();

        builder.Property(h => h.DataRef)
            .HasColumnName("data_ref")
            .IsRequired();

        builder.Property(h => h.Campo)
            .HasColumnName("campo")
            .IsRequired();

        builder.Property(h => h.Fonte)
            .HasColumnName("fonte")
            .IsRequired();

        builder.Property(h => h.Valor)
            .HasColumnName("valor")
            .HasPrecision(SchemaNumericLimits.PrecoPrecisao, SchemaNumericLimits.PrecoEscala)
            .IsRequired();

        builder.Property(h => h.Revisao)
            .HasColumnName("revisao")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(h => h.ObservadoEm)
            .HasColumnName("observado_em")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(h => new { h.InstrumentoId, h.DataRef })
            .IsDescending(false, true)
            .HasDatabaseName("ix_historico_precos_lookup");
    }
}

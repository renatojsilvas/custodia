using Custodia.Domain.Common;
using Custodia.Domain.Precos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Custodia.Infrastructure.Persistence.Configurations;

public sealed class PrecoAtualConfiguration : IEntityTypeConfiguration<PrecoAtual>
{
    public void Configure(EntityTypeBuilder<PrecoAtual> builder)
    {
        builder.ToTable("preco_atual");

        builder.HasKey(p => p.InstrumentoId);

        builder.Property(p => p.InstrumentoId)
            .HasColumnName("instrumento_id")
            .IsRequired();

        builder.Property(p => p.DataRef)
            .HasColumnName("data_ref")
            .IsRequired();

        builder.Property(p => p.Campo)
            .HasColumnName("campo")
            .IsRequired();

        builder.Property(p => p.Valor)
            .HasColumnName("valor")
            .HasPrecision(SchemaNumericLimits.PrecoPrecisao, SchemaNumericLimits.PrecoEscala)
            .IsRequired();

        builder.Property(p => p.Revisao)
            .HasColumnName("revisao")
            .IsRequired();
    }
}

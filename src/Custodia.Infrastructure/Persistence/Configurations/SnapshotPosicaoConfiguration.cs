using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Custodia.Infrastructure.Persistence.Configurations;

public sealed class SnapshotPosicaoConfiguration : IEntityTypeConfiguration<SnapshotPosicao>
{
    public void Configure(EntityTypeBuilder<SnapshotPosicao> builder)
    {
        builder.ToTable("snapshots_posicao");

        builder.HasKey(s => new { s.ClienteId, s.InstrumentoId, s.Data, s.CalculadoEm });

        builder.Property(s => s.ClienteId)
            .HasColumnName("cliente_id")
            .IsRequired();

        builder.Property(s => s.InstrumentoId)
            .HasColumnName("instrumento_id")
            .IsRequired();

        builder.Property(s => s.Data)
            .HasColumnName("data")
            .IsRequired();

        builder.Property(s => s.Quantidade)
            .HasColumnName("quantidade")
            .HasPrecision(SchemaNumericLimits.QuantidadePrecisao, SchemaNumericLimits.QuantidadeEscala)
            .IsRequired();

        builder.Property(s => s.Preco)
            .HasColumnName("preco")
            .HasPrecision(SchemaNumericLimits.PrecoPrecisao, SchemaNumericLimits.PrecoEscala)
            .IsRequired();

        builder.Property(s => s.Valor)
            .HasColumnName("valor")
            .HasPrecision(SchemaNumericLimits.ValorPrecisao, SchemaNumericLimits.ValorEscala)
            .IsRequired();

        builder.Property(s => s.PrecoMedio)
            .HasColumnName("preco_medio")
            .HasPrecision(SchemaNumericLimits.PrecoPrecisao, SchemaNumericLimits.PrecoEscala)
            .IsRequired(false);

        builder.Property(s => s.Custo)
            .HasColumnName("custo")
            .HasPrecision(SchemaNumericLimits.ValorPrecisao, SchemaNumericLimits.ValorEscala)
            .IsRequired(false);

        builder.Property(s => s.CalculadoEm)
            .HasColumnName("calculado_em")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(s => s.Vigente)
            .HasColumnName("vigente")
            .HasDefaultValue(true)
            .IsRequired();
    }
}

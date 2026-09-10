using Custodia.Domain.Common;
using Custodia.Domain.Posicoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Custodia.Infrastructure.Persistence.Configurations;

public sealed class PosicaoCorrenteConfiguration : IEntityTypeConfiguration<PosicaoCorrente>
{
    public void Configure(EntityTypeBuilder<PosicaoCorrente> builder)
    {
        builder.ToTable("posicao_corrente");

        builder.HasKey(p => new { p.ClienteId, p.InstrumentoId });

        builder.Property(p => p.ClienteId)
            .HasColumnName("cliente_id")
            .IsRequired();

        builder.Property(p => p.InstrumentoId)
            .HasColumnName("instrumento_id")
            .IsRequired();

        builder.Property(p => p.Quantidade)
            .HasColumnName("quantidade")
            .HasPrecision(SchemaNumericLimits.QuantidadePrecisao, SchemaNumericLimits.QuantidadeEscala)
            .IsRequired();

        builder.Property(p => p.PrecoMedio)
            .HasColumnName("preco_medio")
            .HasPrecision(SchemaNumericLimits.PrecoPrecisao, SchemaNumericLimits.PrecoEscala)
            .IsRequired(false);

        builder.Property(p => p.CustoTotal)
            .HasColumnName("custo_total")
            .HasPrecision(SchemaNumericLimits.ValorPrecisao, SchemaNumericLimits.ValorEscala)
            .IsRequired(false);
    }
}

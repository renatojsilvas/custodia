using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Custodia.Infrastructure.Persistence.Configurations;

public sealed class MovimentoConfiguration : IEntityTypeConfiguration<Movimento>
{
    public void Configure(EntityTypeBuilder<Movimento> builder)
    {
        builder.ToTable("movimentos", t =>
        {
            t.HasTrigger("trg_movimentos_imutavel");
            t.HasTrigger("trg_movimentos_data_evento_futura");

            t.HasCheckConstraint(
                "ck_movimentos_tipo_valido",
                $"tipo IN ({string.Join(", ", TipoMovimento.All.Select(tipo => $"'{tipo.Name}'"))})");

            t.HasCheckConstraint(
                "ck_movimentos_ajuste_coerente",
                "(tipo = 'ajuste') = (ref_estorno IS NOT NULL)");

            t.HasCheckConstraint(
                "ck_movimentos_estorno_nao_auto",
                "ref_estorno IS NULL OR ref_estorno <> id");

            t.HasCheckConstraint(
                "ck_movimentos_ref_externa_nao_vazia",
                "btrim(ref_externa) <> ''");

            t.HasCheckConstraint(
                "ck_movimentos_instrumento_caixa_valido",
                $"lower(btrim(instrumento_id)) NOT LIKE '{InstrumentosCaixa.Prefixo}%' OR instrumento_id IN " +
                $"({string.Join(", ", InstrumentosCaixa.Todos.Select(id => $"'{id}'"))})");

            t.HasCheckConstraint(
                "ck_movimentos_valor_nao_negativo",
                "tipo = 'ajuste' OR valor_financeiro >= 0");

            t.HasCheckConstraint(
                "ck_movimentos_cupom_sem_quantidade",
                "tipo <> 'cupom' OR qtd_delta = 0");

            t.HasCheckConstraint(
                "ck_movimentos_cliente_id_nao_vazio",
                "btrim(cliente_id) <> ''");

            t.HasCheckConstraint(
                "ck_movimentos_instrumento_id_nao_vazio",
                "btrim(instrumento_id) <> ''");

            t.HasCheckConstraint(
                "ck_movimentos_cliente_id_sem_espaco_nas_bordas",
                @"cliente_id !~ '^[\s\u00A0\u1680\u2007\u202F]|[\s\u00A0\u1680\u2007\u202F]$'");

            t.HasCheckConstraint(
                "ck_movimentos_instrumento_id_sem_espaco_nas_bordas",
                @"instrumento_id !~ '^[\s\u00A0\u1680\u2007\u202F]|[\s\u00A0\u1680\u2007\u202F]$'");

            t.HasCheckConstraint(
                "ck_movimentos_ref_externa_sem_espaco_nas_bordas",
                @"ref_externa !~ '^[\s\u00A0\u1680\u2007\u202F]|[\s\u00A0\u1680\u2007\u202F]$'");
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(m => m.ClienteId)
            .HasColumnName("cliente_id")
            .IsRequired();

        builder.Property(m => m.InstrumentoId)
            .HasColumnName("instrumento_id")
            .IsRequired();

        builder.Property(m => m.Tipo)
            .HasColumnName("tipo")
            .IsRequired()
            .HasConversion(
                v => v.Name,
                v => TipoMovimento.FromName(v).Value);

        builder.Property(m => m.DataEvento)
            .HasColumnName("data_evento")
            .IsRequired();

        builder.Property(m => m.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(m => m.QtdDelta)
            .HasColumnName("qtd_delta")
            .HasPrecision(SchemaNumericLimits.QuantidadePrecisao, SchemaNumericLimits.QuantidadeEscala)
            .IsRequired();

        builder.Property(m => m.ValorFinanceiro)
            .HasColumnName("valor_financeiro")
            .HasPrecision(SchemaNumericLimits.ValorPrecisao, SchemaNumericLimits.ValorEscala)
            .IsRequired();

        builder.Property(m => m.RefExterna)
            .HasColumnName("ref_externa")
            .IsRequired();

        builder.Property(m => m.RefEstorno)
            .HasColumnName("ref_estorno")
            .IsRequired(false);

        builder.HasAlternateKey(m => new { m.Id, m.ClienteId, m.InstrumentoId })
            .HasName("ux_movimentos_id_cliente_instrumento");

        builder.HasOne<Movimento>()
            .WithMany()
            .HasPrincipalKey(m => new { m.Id, m.ClienteId, m.InstrumentoId })
            .HasForeignKey(m => new { m.RefEstorno, m.ClienteId, m.InstrumentoId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_movimentos_movimentos_ref_estorno");

        builder.HasIndex(m => new { m.RefEstorno, m.ClienteId, m.InstrumentoId })
            .HasDatabaseName("ix_movimentos_ref_estorno_cliente_instrumento");

        builder.HasIndex(m => new { m.ClienteId, m.RefExterna })
            .IsUnique()
            .HasDatabaseName("ix_movimentos_cliente_ref_externa");

        builder.HasIndex(m => m.RefEstorno)
            .IsUnique()
            .HasFilter("ref_estorno IS NOT NULL")
            .HasDatabaseName("ix_movimentos_ref_estorno_unico");

        builder.HasIndex(m => new { m.ClienteId, m.InstrumentoId, m.DataEvento, m.RegistradoEm, m.Id })
            .HasDatabaseName("ix_movimentos_cliente_instrumento_data_evento_registrado_em_id");
    }
}

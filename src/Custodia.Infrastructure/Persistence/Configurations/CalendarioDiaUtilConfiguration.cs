using Custodia.Domain.Calendario;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Custodia.Infrastructure.Persistence.Configurations;

public sealed class CalendarioDiaUtilConfiguration : IEntityTypeConfiguration<CalendarioDiaUtil>
{
    public void Configure(EntityTypeBuilder<CalendarioDiaUtil> builder)
    {
        builder.ToTable("calendario_dias_uteis");

        builder.HasKey(c => c.Data);

        builder.Property(c => c.Data)
            .HasColumnName("data")
            .IsRequired();
    }
}

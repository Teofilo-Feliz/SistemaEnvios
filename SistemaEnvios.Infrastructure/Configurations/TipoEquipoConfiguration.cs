using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class TipoEquipoConfiguration : IEntityTypeConfiguration<TipoEquipo>
{
    public void Configure(EntityTypeBuilder<TipoEquipo> b)
    {
        b.ToTable("TiposEquipo");
        b.HasKey(x => x.TipoEquipoId);
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

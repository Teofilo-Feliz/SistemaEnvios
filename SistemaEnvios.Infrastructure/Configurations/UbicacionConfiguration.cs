using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class UbicacionConfiguration : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> b)
    {
        b.ToTable("Ubicaciones");
        b.HasKey(x => x.UbicacionId);
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.CodigoCentro).HasMaxLength(50).IsRequired();
        b.Property(x => x.Tipo).HasConversion<byte>().IsRequired();
        b.HasIndex(x => x.CodigoCentro).IsUnique();
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
        b.ToTable(t => t.HasCheckConstraint("CK_Ubicacion_Tipo", "Tipo IN (1, 2)"));
    }
}

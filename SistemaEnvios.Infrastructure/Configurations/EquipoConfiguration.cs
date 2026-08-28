using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
{
    public void Configure(EntityTypeBuilder<Equipo> b)
    {
        b.ToTable("Equipos");
        b.HasKey(x => x.EquipoId);
        b.Property(x => x.CodigoActivo).HasMaxLength(50);
        b.Property(x => x.NumeroSerie).HasMaxLength(100);
        b.Property(x => x.Marca).HasMaxLength(100).IsRequired();
        b.Property(x => x.Modelo).HasMaxLength(100).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.HasIndex(x => x.CodigoActivo).IsUnique().HasFilter("[CodigoActivo] IS NOT NULL");
        b.HasIndex(x => x.NumeroSerie).IsUnique().HasFilter("[NumeroSerie] IS NOT NULL");
        b.HasOne(x => x.TipoEquipo).WithMany(x => x.Equipos).HasForeignKey(x => x.TipoEquipoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UbicacionActual).WithMany(x => x.EquiposActuales).HasForeignKey(x => x.UbicacionActualId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

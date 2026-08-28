using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class RecepcionEquipoConfiguration : IEntityTypeConfiguration<RecepcionEquipo>
{
    public void Configure(EntityTypeBuilder<RecepcionEquipo> b)
    {
        b.ToTable("RecepcionEquipos");
        b.HasKey(x => x.RecepcionEquipoId);
        b.HasIndex(x => x.EnvioEquipoId).IsUnique();
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.HasOne(x => x.Recepcion).WithMany(x => x.Equipos).HasForeignKey(x => x.RecepcionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.EnvioEquipo).WithOne(x => x.RecepcionEquipo).HasForeignKey<RecepcionEquipo>(x => x.EnvioEquipoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
        b.ToTable(t => t.HasCheckConstraint("CK_RecepcionEquipos_Estado", "EstadoRecepcionEquipo BETWEEN 1 AND 3"));
        b.ToTable(t => t.HasCheckConstraint("CK_RecepcionEquipos_Incidencia", "EstadoRecepcionEquipo <> 3 OR NULLIF(LTRIM(RTRIM(Observaciones)), N'') IS NOT NULL"));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class IncidenciaConfiguration : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> b)
    {
        b.ToTable("Incidencias");
        b.HasKey(x => x.IncidenciaId);
        b.Property(x => x.Descripcion).HasMaxLength(2000).IsRequired();
        b.HasOne(x => x.Envio).WithMany(x => x.Incidencias).HasForeignKey(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.EnvioEquipo).WithMany(x => x.Incidencias).HasForeignKey(x => x.EnvioEquipoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Transporte).WithMany(x => x.Incidencias).HasForeignKey(x => x.TransporteId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
        b.ToTable(t => t.HasCheckConstraint("CK_Incidencias_UnSoloAlcance", "NOT (EnvioEquipoId IS NOT NULL AND TransporteId IS NOT NULL)"));
    }
}

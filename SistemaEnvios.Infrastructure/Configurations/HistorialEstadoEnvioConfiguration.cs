using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class HistorialEstadoEnvioConfiguration : IEntityTypeConfiguration<HistorialEstadoEnvio>
{
    public void Configure(EntityTypeBuilder<HistorialEstadoEnvio> b)
    {
        b.ToTable("HistorialesEstadoEnvio");
        b.HasKey(x => x.HistorialEstadoEnvioId);
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.HasOne(x => x.Envio).WithMany(x => x.HistorialEstados).HasForeignKey(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.EstadoEnvio).WithMany().HasForeignKey(x => x.EstadoEnvioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Ubicacion).WithMany(x => x.HistorialEstados).HasForeignKey(x => x.UbicacionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

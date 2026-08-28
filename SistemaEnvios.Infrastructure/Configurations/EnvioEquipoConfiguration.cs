using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class EnvioEquipoConfiguration : IEntityTypeConfiguration<EnvioEquipo>
{
    public void Configure(EntityTypeBuilder<EnvioEquipo> b)
    {
        b.ToTable("EnvioEquipos");
        b.HasKey(x => x.EnvioEquipoId);
        b.Property(x => x.NumeroTicket).HasMaxLength(50).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.EnvioId, x.EquipoId }).IsUnique();
        b.HasOne(x => x.Envio).WithMany(x => x.Equipos).HasForeignKey(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Equipo).WithMany(x => x.Envios).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

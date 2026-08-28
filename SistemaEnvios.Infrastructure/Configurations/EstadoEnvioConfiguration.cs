using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class EstadoEnvioConfiguration : IEntityTypeConfiguration<EstadoEnvio>
{
    public void Configure(EntityTypeBuilder<EstadoEnvio> b)
    {
        b.ToTable("EstadosEnvio");
        b.HasKey(x => x.EstadoEnvioId);
        b.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(500);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

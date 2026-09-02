using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class PermisoPosicionConfiguration : IEntityTypeConfiguration<PermisoPosicion>
{
    public void Configure(EntityTypeBuilder<PermisoPosicion> b)
    {
        b.ToTable("PermisosPorPosicion");
        b.HasKey(x => new { x.Posicion, x.Permiso });
        b.Property(x => x.Posicion).HasMaxLength(150);
        b.Property(x => x.Permiso).HasMaxLength(50);
    }
}

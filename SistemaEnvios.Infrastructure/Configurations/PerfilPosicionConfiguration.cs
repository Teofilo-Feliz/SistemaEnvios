using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class PerfilPosicionConfiguration : IEntityTypeConfiguration<PerfilPosicion>
{
    public void Configure(EntityTypeBuilder<PerfilPosicion> b)
    {
        b.ToTable("PerfilesPorPosicion");
        b.HasKey(x => x.Posicion);
        b.Property(x => x.Posicion).HasMaxLength(150);
        b.ToTable(t => t.HasCheckConstraint("CK_PerfilPosicion_Perfil", "Perfil IN (1, 2, 3)"));
    }
}

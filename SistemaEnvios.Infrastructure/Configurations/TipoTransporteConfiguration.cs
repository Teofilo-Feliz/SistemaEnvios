using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class TipoTransporteConfiguration : IEntityTypeConfiguration<TipoTransporte>
{
    public void Configure(EntityTypeBuilder<TipoTransporte> b)
    {
        b.ToTable("TiposTransporte", t => t.HasCheckConstraint("CK_TiposTransporte_Estrategia", "Estrategia IN (1, 2)"));
        b.HasKey(x => x.TipoTransporteId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class TransporteConfiguration : IEntityTypeConfiguration<Transporte>
{
    public void Configure(EntityTypeBuilder<Transporte> b)
    {
        b.ToTable("Transportes");
        b.HasKey(x => x.TransporteId);
        b.HasIndex(x => x.EnvioId).IsUnique();
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Envio).WithOne(x => x.Transporte).HasForeignKey<Transporte>(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TipoTransporte).WithMany(x => x.Transportes).HasForeignKey(x => x.TipoTransporteId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

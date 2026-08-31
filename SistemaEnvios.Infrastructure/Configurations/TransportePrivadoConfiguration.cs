using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class TransportePrivadoConfiguration : IEntityTypeConfiguration<TransportePrivado>
{
    public void Configure(EntityTypeBuilder<TransportePrivado> b)
    {
        b.ToTable("TransportesPrivados");
        b.HasKey(x => x.TransporteId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.NombreResponsable).HasMaxLength(150).IsRequired();
        b.Property(x => x.Parentesco).HasMaxLength(50).IsRequired();
        b.Property(x => x.CedulaResponsable).HasMaxLength(11).IsRequired();
        b.Property(x => x.PlacaVehiculo).HasMaxLength(20).IsRequired();
        b.HasOne(x => x.Transporte).WithOne(x => x.Privado).HasForeignKey<TransportePrivado>(x => x.TransporteId).OnDelete(DeleteBehavior.Cascade);
    }
}

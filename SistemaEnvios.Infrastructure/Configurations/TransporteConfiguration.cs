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
        b.Property(x => x.Tipo).HasMaxLength(50).IsRequired();
        b.Property(x => x.NombreChofer).HasMaxLength(150);
        b.Property(x => x.Placa).HasMaxLength(20);
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.HasOne(x => x.Envio).WithOne(x => x.Transporte).HasForeignKey<Transporte>(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.ToTable(t => t.HasCheckConstraint("CK_Transporte_Confirmacion", "(EntregaConfirmada = 0 AND FechaConfirmacionEntrega IS NULL AND UsuarioConfirmacionId IS NULL) OR (EntregaConfirmada = 1 AND FechaConfirmacionEntrega IS NOT NULL AND UsuarioConfirmacionId IS NOT NULL)"));
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

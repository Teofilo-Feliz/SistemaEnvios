using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class TransporteInternoConfiguration : IEntityTypeConfiguration<TransporteInterno>
{
    public void Configure(EntityTypeBuilder<TransporteInterno> b)
    {
        b.ToTable("TransportesInternos", t => t.HasCheckConstraint("CK_TransporteInterno_Confirmacion", "(EntregaConfirmada = 0 AND FechaConfirmacionEntrega IS NULL AND UsuarioConfirmacionId IS NULL) OR (EntregaConfirmada = 1 AND FechaEntregaTransportacion IS NOT NULL AND FechaConfirmacionEntrega IS NOT NULL AND FechaConfirmacionEntrega >= FechaEntregaTransportacion AND UsuarioConfirmacionId IS NOT NULL)"));
        b.HasKey(x => x.TransporteId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.NombreChoferAlMomento).HasMaxLength(150).IsRequired();
        b.Property(x => x.NumeroEmpleadoAlMomento).HasMaxLength(30).IsRequired();
        b.HasOne(x => x.Transporte).WithOne(x => x.Interno).HasForeignKey<TransporteInterno>(x => x.TransporteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ChoferInterno).WithMany(x => x.Transportes).HasForeignKey(x => x.ChoferInternoId).OnDelete(DeleteBehavior.Restrict);
    }
}

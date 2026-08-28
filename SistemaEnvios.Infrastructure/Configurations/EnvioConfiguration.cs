using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class EnvioConfiguration : IEntityTypeConfiguration<Envio>
{
    public void Configure(EntityTypeBuilder<Envio> b)
    {
        b.ToTable("Envios");
        b.HasKey(x => x.EnvioId);
        b.Property(x => x.NumeroEnvio).HasMaxLength(30).IsRequired();
        b.Property(x => x.Direccion).HasConversion<byte>().IsRequired();
        b.HasIndex(x => x.NumeroEnvio).IsUnique();
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.UbicacionOrigen).WithMany(x => x.EnviosOrigen).HasForeignKey(x => x.UbicacionOrigenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UbicacionDestino).WithMany(x => x.EnviosDestino).HasForeignKey(x => x.UbicacionDestinoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EstadoEnvio).WithMany(x => x.Envios).HasForeignKey(x => x.EstadoEnvioId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
        b.ToTable(t => t.HasCheckConstraint("CK_Envio_UbicacionesDistintas", "UbicacionOrigenId <> UbicacionDestinoId"));
        b.ToTable(t => t.HasCheckConstraint("CK_Envio_Direccion", "Direccion IN (1, 2)"));
    }
}

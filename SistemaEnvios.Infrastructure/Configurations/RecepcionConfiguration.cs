using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class RecepcionConfiguration : IEntityTypeConfiguration<Recepcion>
{
    public void Configure(EntityTypeBuilder<Recepcion> b)
    {
        b.ToTable("Recepciones");
        b.HasKey(x => x.RecepcionId);
        b.HasIndex(x => x.EnvioId).IsUnique();
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.Property(x => x.TecnicoAsignadoNombre).HasMaxLength(150);
        b.Property(x => x.TecnicoAsignadoNumeroEmpleado).HasMaxLength(30);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.TecnicoAsignado).WithMany(x => x.RecepcionesAsignadas).HasForeignKey(x => x.TecnicoAsignadoUsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Envio).WithOne(x => x.Recepcion).HasForeignKey<Recepcion>(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
        b.ToTable(t => t.HasCheckConstraint("CK_Recepciones_Estado", "EstadoRecepcion BETWEEN 1 AND 5"));
    }
}

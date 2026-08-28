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
        b.HasOne(x => x.Envio).WithOne(x => x.Recepcion).HasForeignKey<Recepcion>(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

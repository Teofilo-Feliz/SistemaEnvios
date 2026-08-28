using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class ReservaEquipoEnvioConfiguration : IEntityTypeConfiguration<ReservaEquipoEnvio>
{
    public void Configure(EntityTypeBuilder<ReservaEquipoEnvio> builder)
    {
        builder.ToTable("ReservasEquipoEnvio");
        builder.HasKey(x => x.EquipoId);
        builder.HasIndex(x => x.EnvioId);
        builder.HasOne(x => x.Equipo)
            .WithMany()
            .HasForeignKey(x => x.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Envio)
            .WithMany()
            .HasForeignKey(x => x.EnvioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

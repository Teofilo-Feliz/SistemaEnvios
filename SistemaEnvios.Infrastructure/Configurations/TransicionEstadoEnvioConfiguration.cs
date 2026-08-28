using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class TransicionEstadoEnvioConfiguration : IEntityTypeConfiguration<TransicionEstadoEnvio>
{
    public void Configure(EntityTypeBuilder<TransicionEstadoEnvio> b)
    {
        b.ToTable("TransicionesEstadoEnvio");
        b.HasKey(x => x.TransicionEstadoEnvioId);
        b.HasIndex(x => new { x.EstadoOrigenId, x.EstadoDestinoId }).IsUnique();
        b.HasOne(x => x.EstadoOrigen).WithMany(x => x.TransicionesOrigen).HasForeignKey(x => x.EstadoOrigenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EstadoDestino).WithMany(x => x.TransicionesDestino).HasForeignKey(x => x.EstadoDestinoId).OnDelete(DeleteBehavior.Restrict);
    }
}

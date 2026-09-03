using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;
namespace SistemaEnvios.Infrastructure.Configurations;
public sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones"); b.HasKey(x=>x.NotificacionId);
        b.Property(x=>x.Tipo).HasMaxLength(50).IsRequired(); b.Property(x=>x.Titulo).HasMaxLength(200).IsRequired();
        b.Property(x=>x.Mensaje).HasMaxLength(1000).IsRequired(); b.Property(x=>x.DestinatarioRol).HasMaxLength(50).IsRequired();
        b.HasIndex(x=>new{x.DestinatarioRol,x.FechaCreacion});
        b.HasOne(x=>x.Envio).WithMany().HasForeignKey(x=>x.EnvioId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;
public sealed class UsuarioReferenciaConfiguration : IEntityTypeConfiguration<UsuarioReferencia>
{
    public void Configure(EntityTypeBuilder<UsuarioReferencia> b)
    {
        b.ToTable("UsuariosReferencia"); b.HasKey(x => x.UsuarioExternoId);
        b.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
        b.Property(x => x.NumeroEmpleado).HasMaxLength(30); b.Property(x => x.Correo).HasMaxLength(254);
        b.Property(x => x.Origen).HasMaxLength(30).IsRequired(); b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.NumeroEmpleado).IsUnique().HasFilter("[NumeroEmpleado] IS NOT NULL");
        b.HasIndex(x => new { x.EsTecnico, x.Activo, x.NombreCompleto });
    }
}

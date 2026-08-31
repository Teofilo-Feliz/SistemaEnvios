using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public sealed class ChoferInternoConfiguration : IEntityTypeConfiguration<ChoferInterno>
{
    public void Configure(EntityTypeBuilder<ChoferInterno> b)
    {
        b.ToTable("ChoferesInternos");
        b.HasKey(x => x.ChoferInternoId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.NumeroEmpleado).IsUnique();
        b.Property(x => x.NombreCompleto).HasMaxLength(150).IsRequired();
        b.Property(x => x.NumeroEmpleado).HasMaxLength(30).IsRequired();
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Configurations;

public class EnvioEquipoConfiguration : IEntityTypeConfiguration<EnvioEquipo>
{
    public void Configure(EntityTypeBuilder<EnvioEquipo> b)
    {
        b.ToTable("EnvioEquipos");
        b.HasKey(x => x.EnvioEquipoId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.NumeroTicket).HasMaxLength(50).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(2000).IsRequired();
        b.Property(x => x.MotivoCierreCaso).HasMaxLength(300);
        b.HasIndex(x => new { x.EnvioId, x.EquipoId }).IsUnique();

        // El ticket es único entre las aperturas, no entre todos los movimientos: un caso puede
        // abarcar varios viajes y todos repiten su ticket a propósito. Sin el filtro, EF y el
        // índice de la base dirían cosas distintas sobre lo mismo.
        b.HasIndex(x => x.NumeroTicket)
            .IsUnique()
            .HasFilter("[EnvioEquipoOrigenId] IS NULL")
            .HasDatabaseName("UX_EnvioEquipos_TicketApertura");

        b.HasOne(x => x.Envio).WithMany(x => x.Equipos).HasForeignKey(x => x.EnvioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Equipo).WithMany(x => x.Envios).HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);

        // La cadena del caso. Hay que declarar la clave foránea explícitamente: por convención
        // EF inventa una columna propia a partir del nombre de la navegación (OrigenEnvioEquipoId)
        // y deja EnvioEquipoOrigenId como un campo suelto que nadie escribe.
        b.HasOne(x => x.Origen)
            .WithMany(x => x.Continuaciones)
            .HasForeignKey(x => x.EnvioEquipoOrigenId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaCreacion).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

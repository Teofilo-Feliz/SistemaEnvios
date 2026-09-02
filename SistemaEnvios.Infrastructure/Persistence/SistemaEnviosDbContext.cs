using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Infrastructure.Persistence;

public class SistemaEnviosDbContext(DbContextOptions<SistemaEnviosDbContext> options) : DbContext(options)
{
    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<EnvioEquipo> EnvioEquipos => Set<EnvioEquipo>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<EstadoEnvio> EstadosEnvio => Set<EstadoEnvio>();
    public DbSet<TransicionEstadoEnvio> TransicionesEstadoEnvio => Set<TransicionEstadoEnvio>();
    public DbSet<HistorialEstadoEnvio> HistorialEstadosEnvio => Set<HistorialEstadoEnvio>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<Recepcion> Recepciones => Set<Recepcion>();
    public DbSet<RecepcionEquipo> RecepcionEquipos => Set<RecepcionEquipo>();
    public DbSet<TipoEquipo> TiposEquipo => Set<TipoEquipo>();
    public DbSet<Transporte> Transportes => Set<Transporte>();
    public DbSet<TipoTransporte> TiposTransporte => Set<TipoTransporte>();
    public DbSet<ChoferInterno> ChoferesInternos => Set<ChoferInterno>();
    public DbSet<TransporteInterno> TransportesInternos => Set<TransporteInterno>();
    public DbSet<TransportePrivado> TransportesPrivados => Set<TransportePrivado>();
    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();
    public DbSet<ReservaEquipoEnvio> ReservasEquipoEnvio => Set<ReservaEquipoEnvio>();
    public DbSet<UsuarioReferencia> UsuariosReferencia => Set<UsuarioReferencia>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<PermisoPosicion> PermisosPorPosicion => Set<PermisoPosicion>();
    public DbSet<PerfilPosicion> PerfilesPorPosicion => Set<PerfilPosicion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SistemaEnviosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

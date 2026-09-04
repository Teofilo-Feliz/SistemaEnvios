using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

    /// <summary>
    /// Todo se guarda en UTC, pero SQL Server devuelve las fechas sin zona horaria. Sin esto el
    /// JSON sale sin la "Z" y el navegador interpreta la hora como local: un movimiento de las
    /// 18:01 aparece a las 22:01. Se marca al leer, en un solo sitio, en vez de recordarlo en
    /// cada consulta.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> ALecturaUtc = new(
        aLaBase => aLaBase,
        deLaBase => DateTime.SpecifyKind(deLaBase, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> ALecturaUtcOpcional = new(
        aLaBase => aLaBase,
        deLaBase => deLaBase.HasValue ? DateTime.SpecifyKind(deLaBase.Value, DateTimeKind.Utc) : deLaBase);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SistemaEnviosDbContext).Assembly);

        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
            foreach (var propiedad in entidad.GetProperties())
            {
                if (propiedad.ClrType == typeof(DateTime)) propiedad.SetValueConverter(ALecturaUtc);
                else if (propiedad.ClrType == typeof(DateTime?)) propiedad.SetValueConverter(ALecturaUtcOpcional);
            }

        base.OnModelCreating(modelBuilder);
    }
}

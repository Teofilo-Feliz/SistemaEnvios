using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// El proveedor InMemory acepta un Skip/Take sin ORDER BY; SQL Server lo rechaza con
/// "Invalid usage of the option NEXT in the FETCH statement". Esa diferencia ya nos costó dos
/// errores de migración que ninguna prueba vio, así que cada consulta paginada se ejecuta
/// también contra el motor real.
///
/// Se activa con la cadena en SISTEMAENVIOS_TEST_SQL; sin ella las pruebas se omiten para que
/// la suite siga corriendo en una máquina sin base de datos.
/// </summary>
public sealed class PaginacionSqlServerTests
{
    private sealed class Parametros : ParametrosPagina;

    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    public static TheoryData<string> Consultas =>
    [
        "Ubicaciones", "Equipos", "Envios", "EstadosEnvio", "TiposEquipo",
        "TiposTransporte", "ChoferesInternos", "Incidencias", "Notificaciones",
        "HistorialEstadosEnvio", "EnvioEquipos", "TransicionesEstadoEnvio"
    ];

    [SkippableTheory]
    [MemberData(nameof(Consultas))]
    public async Task CadaListadoSePaginaEnElMotorReal(string tabla)
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");

        await using var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);

        var pagina = await ConsultarAsync(db, tabla, new Parametros { Page = 1, PageSize = 5 });

        Assert.True(pagina.PageSize <= ParametrosPagina.TamanoMaximo);
        Assert.True(pagina.Items.Count <= 5, $"{tabla} devolvió más filas que el tamaño de página.");
        Assert.True(pagina.TotalItems >= pagina.Items.Count);
    }

    /// <summary>
    /// Cada entidad se consulta de verdad contra SQL Server. InMemory no valida nombres de
    /// columna: si el modelo mapea una que la base no tiene —o EF inventa una clave foránea por
    /// convención— todo pasa en verde y revienta en la primera pantalla. Ya ocurrió dos veces.
    /// </summary>
    [SkippableFact]
    public async Task ElModeloCoincideConElEsquemaReal()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");

        await using var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);

        var fallos = new List<string>();
        foreach (var entidad in db.Model.GetEntityTypes().Where(x => !x.IsOwned()))
        {
            try
            {
                // Trae una fila proyectando todas las columnas mapeadas: basta para que SQL
                // Server rechace cualquiera que no exista.
                var consulta = (IQueryable<object>)typeof(DbContext)
                    .GetMethod(nameof(DbContext.Set), 1, [])!
                    .MakeGenericMethod(entidad.ClrType)
                    .Invoke(db, null)!;
                await consulta.Take(1).ToListAsync();
            }
            catch (Exception ex)
            {
                fallos.Add($"{entidad.ClrType.Name}: {ex.GetBaseException().Message}");
            }
        }

        Assert.True(fallos.Count == 0, "El modelo no coincide con el esquema:\n" + string.Join("\n", fallos));
    }

    private static Task<PaginaResponse<int>> ConsultarAsync(SistemaEnviosDbContext db, string tabla, ParametrosPagina p) => tabla switch
    {
        "Ubicaciones" => db.Ubicaciones.AsNoTracking().OrderBy(x => x.Nombre).ThenBy(x => x.UbicacionId).PaginarAsync(p, x => x.UbicacionId, default),
        "Equipos" => db.Equipos.AsNoTracking().OrderBy(x => x.Marca).ThenBy(x => x.Modelo).ThenBy(x => x.EquipoId).PaginarAsync(p, x => x.EquipoId, default),
        "Envios" => db.Envios.AsNoTracking().OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.EnvioId).PaginarAsync(p, x => x.EnvioId, default),
        "EstadosEnvio" => db.EstadosEnvio.AsNoTracking().OrderBy(x => x.Nombre).ThenBy(x => x.EstadoEnvioId).PaginarAsync(p, x => x.EstadoEnvioId, default),
        "TiposEquipo" => db.TiposEquipo.AsNoTracking().OrderBy(x => x.Nombre).ThenBy(x => x.TipoEquipoId).PaginarAsync(p, x => x.TipoEquipoId, default),
        "TiposTransporte" => db.TiposTransporte.AsNoTracking().OrderBy(x => x.Nombre).ThenBy(x => x.TipoTransporteId).PaginarAsync(p, x => x.TipoTransporteId, default),
        "ChoferesInternos" => db.ChoferesInternos.AsNoTracking().OrderBy(x => x.NombreCompleto).ThenBy(x => x.ChoferInternoId).PaginarAsync(p, x => x.ChoferInternoId, default),
        "Incidencias" => db.Incidencias.AsNoTracking().OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.IncidenciaId).PaginarAsync(p, x => x.IncidenciaId, default),
        "Notificaciones" => db.Notificaciones.AsNoTracking().OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.NotificacionId).PaginarAsync(p, x => (int)x.NotificacionId, default),
        "HistorialEstadosEnvio" => db.HistorialEstadosEnvio.AsNoTracking().OrderBy(x => x.Fecha).ThenBy(x => x.HistorialEstadoEnvioId).PaginarAsync(p, x => x.HistorialEstadoEnvioId, default),
        "EnvioEquipos" => db.EnvioEquipos.AsNoTracking().OrderBy(x => x.EnvioEquipoId).PaginarAsync(p, x => x.EnvioEquipoId, default),
        "TransicionesEstadoEnvio" => db.TransicionesEstadoEnvio.AsNoTracking().OrderBy(x => x.EstadoOrigenId).ThenBy(x => x.EstadoDestinoId).PaginarAsync(p, x => x.TransicionEstadoEnvioId, default),
        _ => throw new ArgumentOutOfRangeException(nameof(tabla), tabla, "Tabla sin consulta definida.")
    };
}

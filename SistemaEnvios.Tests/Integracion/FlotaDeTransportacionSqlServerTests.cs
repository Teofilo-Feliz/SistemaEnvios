using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Api.Controllers.Catalogos;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Los tipos de transporte y los choferes internos son de Transportación, no de Tecnología, así
/// que su mantenimiento se mudó al módulo de Transportación.
///
/// El permiso no podía ser ninguno de los que ya existían. 'catalogos.administrar' le habría
/// dado a Transportación todos los catálogos del sistema —filiales, ubicaciones, tipos de
/// equipo—, y 'transportes.gestionar' lo tienen los 34 administradores de filial y los
/// asistentes, porque lo necesitan para asignar transporte a sus propios envíos: reusarlo
/// habría dejado la flota en manos de cualquier filial. De ahí 'transportes.administrar'.
/// </summary>
public sealed class FlotaDeTransportacionSqlServerTests
{
    private const string RolTransportacion = "Encargado Transportación";
    private const string PosicionTransportacion = "Encargado transportacion y mecanica";

    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    /// <summary>
    /// Escribir en la flota exige el permiso nuevo. Sin esto la pantalla se abre pero cada botón
    /// responde 403, que es peor que no tenerla.
    /// </summary>
    [Theory]
    [InlineData(nameof(TransportesCatalogosController.CrearTipo))]
    [InlineData(nameof(TransportesCatalogosController.ActualizarTipo))]
    [InlineData(nameof(TransportesCatalogosController.TipoActivo))]
    [InlineData(nameof(TransportesCatalogosController.CrearChofer))]
    [InlineData(nameof(TransportesCatalogosController.ActualizarChofer))]
    [InlineData(nameof(TransportesCatalogosController.ChoferActivo))]
    public void EscribirEnLaFlotaExigeElPermisoDeTransportacion(string metodo)
    {
        var politica = typeof(TransportesCatalogosController)
            .GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(x => x.Policy)
            .SingleOrDefault();

        Assert.Equal(PermissionNames.TransportesAdministrar, politica);
    }

    [SkippableFact]
    public async Task TransportacionPuedeMantenerSuFlota()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        Assert.Contains(PermissionNames.TransportesAdministrar, await PermisosAsync(db, RolTransportacion));
        Assert.Contains(PermissionNames.TransportesAdministrar, await PermisosAsync(db, PosicionTransportacion));
    }

    [SkippableFact]
    public async Task TecnologiaLoConservaPorSerDuenaDelSistema()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        Assert.Contains(PermissionNames.TransportesAdministrar, await PermisosAsync(db, "Programador Senior"));
    }

    /// <summary>
    /// La razón de que el permiso sea nuevo: quien asigna transporte a un envío no administra la
    /// flota. Si esto se rompe, 34 filiales pueden dar de baja a los choferes de todo el país.
    /// </summary>
    [SkippableTheory]
    [InlineData("Administrador de Filial")]
    [InlineData("Asistente Administrativo")]
    public async Task LasFilialesNoAdministranLaFlota(string clave)
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var permisos = await PermisosAsync(db, clave);

        // Conserva lo suyo: asignar transporte a sus propios envíos.
        Assert.Contains(PermissionNames.TransportesGestionar, permisos);
        Assert.DoesNotContain(PermissionNames.TransportesAdministrar, permisos);
    }

    [SkippableFact]
    public async Task TransportacionSigueSinAdministrarElRestoDeCatalogos()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        // Mudar la flota no la convierte en dueña de filiales, ubicaciones ni tipos de equipo.
        Assert.DoesNotContain(PermissionNames.CatalogosAdministrar, await PermisosAsync(db, RolTransportacion));
    }

    private static async Task<List<string>> PermisosAsync(SistemaEnviosDbContext db, string clave) =>
        await db.PermisosPorPosicion.AsNoTracking()
            .Where(x => x.Posicion == clave)
            .Select(x => x.Permiso)
            .ToListAsync();

    private static SistemaEnviosDbContext Contexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);
}

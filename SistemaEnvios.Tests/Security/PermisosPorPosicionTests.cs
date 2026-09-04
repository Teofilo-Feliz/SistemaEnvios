using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Api.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// AuthManager emite permisos de otra aplicación ("evaluador") y un rol genérico
/// ("Administrador"). Los permisos de este sistema se derivan de la posición del usuario
/// contra PermisosPorPosicion, y se inyectan como claims para que [Authorize] los vea.
/// </summary>
public sealed class PermisosPorPosicionTests
{
    [Fact]
    public async Task Posicion_Mapeada_OtorgaSusPermisos()
    {
        await using var db = await CrearContextoAsync(
            ("Asistente Administrativo", PermissionNames.EnviosConsultar),
            ("Asistente Administrativo", PermissionNames.EnviosCrear));

        var permisos = await TransformarAsync(db, "Asistente Administrativo", "evaluador");

        Assert.Contains(PermissionNames.EnviosConsultar, permisos);
        Assert.Contains(PermissionNames.EnviosCrear, permisos);
    }

    [Fact]
    public async Task PermisoAjeno_DelTokenNoSePropaga()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PermissionNames.EnviosConsultar));

        var permisos = await TransformarAsync(db, "Asistente Administrativo", "evaluador");

        Assert.DoesNotContain("evaluador", permisos);
    }

    [Fact]
    public async Task PermisoPropio_DelTokenSeConserva()
    {
        await using var db = await CrearContextoAsync();

        var permisos = await TransformarAsync(db, "Cargo Desconocido", PermissionNames.CatalogosAdministrar);

        Assert.Equal([PermissionNames.CatalogosAdministrar], permisos);
    }

    [Fact]
    public async Task PosicionSinMapeo_NoOtorgaNada()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PermissionNames.EnviosConsultar));

        var permisos = await TransformarAsync(db, "Cargo Desconocido", "evaluador");

        Assert.Empty(permisos);
    }

    [Fact]
    public async Task Posicion_SeComparaSinDistinguirEspaciosSobrantes()
    {
        await using var db = await CrearContextoAsync(("Encargado de Transportacion", PermissionNames.TransportesConfirmar));

        var permisos = await TransformarAsync(db, "  Encargado de Transportacion  ", "");

        Assert.Contains(PermissionNames.TransportesConfirmar, permisos);
    }

    [Fact]
    public async Task Transformar_DosVeces_NoDuplicaPermisos()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PermissionNames.EnviosConsultar));
        var transformacion = new PermisosPorPosicionTransformation(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<PermisosPorPosicionTransformation>.Instance);
        var principal = Principal("Asistente Administrativo", "evaluador");

        var unaVez = await transformacion.TransformAsync(principal);
        var dosVeces = await transformacion.TransformAsync(unaVez);

        Assert.Single(dosVeces.FindAll("permissions").Where(x => x.Value == PermissionNames.EnviosConsultar));
    }

    private static async Task<string[]> TransformarAsync(SistemaEnviosDbContext db, string posicion, string permisos)
    {
        var transformacion = new PermisosPorPosicionTransformation(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<PermisosPorPosicionTransformation>.Instance);
        var resultado = await transformacion.TransformAsync(Principal(posicion, permisos));
        return resultado.FindAll("permissions").Select(x => x.Value).ToArray();
    }

    private static ClaimsPrincipal Principal(string posicion, string permisos)
    {
        List<Claim> claims = [new("position", posicion), new("roles", "Administrador")];
        if (!string.IsNullOrEmpty(permisos)) claims.Add(new Claim("permissions", permisos));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Prueba"));
    }

    private static async Task<SistemaEnviosDbContext> CrearContextoAsync(params (string Posicion, string Permiso)[] mapeo)
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.PermisosPorPosicion.AddRange(mapeo.Select(x => new PermisoPosicion { Posicion = x.Posicion, Permiso = x.Permiso }));
        await db.SaveChangesAsync();
        return db;
    }
}

using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// El permiso "alcance.global" concede vista global a cualquier rol que lo traiga.
///
/// Existe por un límite de AuthManager: un rol se ata a un solo grupo de seguridad, así que darle
/// vista global a un rol que ya existe obligaba a crear otro rol y sembrar su fila en
/// PerfilesPorPosicion. Con el permiso se concede desde AuthManager, sin tocar la base.
///
/// Llega por las dos vías sin que estas pruebas distingan: AuthManager puede emitirlo en el claim
/// "permissions", y PermisosPorPosicionTransformation puede derivarlo de PermisosPorPosicion. Las
/// dos acaban en IUserContext.Permissions, que es lo que se lee aquí.
/// </summary>
public sealed class AlcanceGlobalPorPermisoTests
{
    private static readonly Guid UsuarioId = Guid.Parse("c41d9a77-3e52-4b08-9f16-2d7a8e5c0b34");

    [Fact]
    public async Task ElPermisoConcedeGlobalAunqueElRolSeaDeFilial()
    {
        await using var db = await SembrarAsync();
        var usuario = Usuario(["Administrador de Filial"], [PermissionNames.AlcanceGlobal]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    /// <summary>
    /// Suma, no sustituye. Es la misma regla que rige entre roles: conceder algo nunca quita lo
    /// que ya se tenía. Si restara, dar el permiso a alguien de Tecnología lo dejaría peor.
    /// </summary>
    [Fact]
    public async Task ElPermisoNoDegradaAQuienYaTeniaMasAlcance()
    {
        await using var db = await SembrarAsync();
        var usuario = Usuario(["Soporte Técnico"], [PermissionNames.AlcanceGlobal]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    /// <summary>
    /// Sin ningún rol mapeado el permiso basta por sí solo. Es el caso de un rol recién creado en
    /// AuthManager al que todavía nadie le sembró fila.
    /// </summary>
    [Fact]
    public async Task ElPermisoBastaSinNingunRolMapeado()
    {
        await using var db = await SembrarAsync();
        var usuario = Usuario(["RolQueNadieRegistro"], [PermissionNames.AlcanceGlobal]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    [Fact]
    public async Task SinElPermisoElRolSigueMandando()
    {
        await using var db = await SembrarAsync();
        var usuario = Usuario(["Administrador de Filial"], [PermissionNames.EnviosConsultar]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Filial, perfil);
    }

    /// <summary>
    /// Solo concede el nombre exacto. Un permiso de otra aplicación que se le parezca no asciende
    /// a nadie: es el mismo fallo que ya tuvimos concediendo Global por el nombre del rol, cuando
    /// un administrador de otra aplicación de AuthManager entraba aquí viéndolo todo.
    /// </summary>
    [Theory]
    [InlineData("alcance.total")]
    [InlineData("global")]
    [InlineData("admin.global")]
    [InlineData("alcanceglobal")]
    public async Task UnPermisoParecidoNoConcedeGlobal(string permiso)
    {
        await using var db = await SembrarAsync();
        var usuario = Usuario(["Administrador de Filial"], [permiso]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Filial, perfil);
    }

    private static IUserContext Usuario(string[] roles, string[] permisos) =>
        new FakeUserContext(UsuarioId, "30,SANTO DOMINGO (SEDE)", roles: roles, permissions: permisos);

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.PerfilesPorPosicion.AddRange(
            new PerfilPosicion { Posicion = "Administrador de Filial", Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = "Soporte Técnico", Perfil = (byte)PerfilAlcance.Tecnologia });
        await db.SaveChangesAsync();
        return db;
    }
}

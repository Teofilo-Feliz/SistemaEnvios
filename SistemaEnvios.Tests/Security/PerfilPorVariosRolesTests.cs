using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// Un usuario puede traer varios roles a la vez, y mapear a perfiles distintos.
///
/// El caso que lo destapó: la encargada de soporte técnico es además super administradora, y su
/// token llega con roles "Soporte Técnico,SuperAdministrador". La consulta tomaba el primero que
/// devolviera SQL Server con un FirstOrDefault SIN ORDER BY, así que el perfil dependía del plan,
/// del índice y del orden físico de las filas: el mismo usuario podía resolver distinto entre
/// despliegues sin que nadie tocara nada.
///
/// Ahora gana el de mayor alcance, con un orden explícito. El criterio es que conceder un rol
/// nunca quite lo que ya se tenía.
/// </summary>
public sealed class PerfilPorVariosRolesTests
{
    private static readonly Guid UsuarioId = Guid.Parse("970889b4-fc6b-4f5b-8371-41de13b2d63a");

    [Fact]
    public async Task ConSoporteTecnicoYSuperAdministradorGanaGlobal()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, "Encargada de Soporte Técnico", 30,
            "Soporte Técnico", "SuperAdministrador");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    /// <summary>El orden en que AuthManager escriba el claim no debe cambiar el resultado.</summary>
    [Fact]
    public async Task ElOrdenDeLosRolesEnElTokenNoImporta()
    {
        await using var db = await SembrarAsync();
        var alDerecho = FakeUserContext.ConRoles(UsuarioId, null, 30, "Soporte Técnico", "SuperAdministrador");
        var alReves = FakeUserContext.ConRoles(UsuarioId, null, 30, "SuperAdministrador", "Soporte Técnico");

        var uno = await AlcanceDePrueba.Crear(db, alDerecho).ResolverPerfilAsync();
        var otro = await AlcanceDePrueba.Crear(db, alReves).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, uno);
        Assert.Equal(uno, otro);
    }

    [Fact]
    public async Task ConUnSoloRolMapeadoDevuelveEseMismo()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, null, 30, "Soporte Técnico");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Tecnologia, perfil);
    }

    /// <summary>Tecnología pasa por delante aunque su número sea mayor: ve los mismos datos.</summary>
    [Fact]
    public async Task TecnologiaGanaATransportacionYFilial()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, null, 30,
            "Administrador de Filial", "Encargado Transportación", "Soporte Técnico");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Tecnologia, perfil);
    }

    [Fact]
    public async Task SinNingunRolMapeadoCaeAlRespaldo()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, null, 30, "RolQueNadieRegistro");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Filial, perfil);
    }

    /// <summary>
    /// La posición y los roles se miran juntos. Antes la posición se consultaba primero y, si
    /// estaba mapeada, el rol no llegaba a contar: a quien administra una filial y además le
    /// conceden el grupo de super administrador, el grupo no le servía de nada.
    /// </summary>
    [Fact]
    public async Task UnRolMasAmplioGanaSobreLaPosicion()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, "Administrador de Filial", 30, "SuperAdministrador");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    /// <summary>
    /// Y al revés: un rol más estrecho no degrada a quien ya tenía más alcance por su posición.
    /// Esa es la trampa que tendría dar prioridad al rol sin mirar el alcance.
    /// </summary>
    [Fact]
    public async Task UnRolMasEstrechoNoDegradaLaPosicion()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, "Programador Senior", 30, "Administrador de Filial");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    /// <summary>Sin roles, la posición sigue resolviendo por sí sola.</summary>
    [Fact]
    public async Task LaPosicionSolaSigueResolviendo()
    {
        await using var db = await SembrarAsync();
        var usuario = FakeUserContext.ConRoles(UsuarioId, "Administrador de Filial", 30);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Filial, perfil);
    }

    // ---------- la regla de precedencia, sin base de datos ----------

    [Fact]
    public void GlobalEsElDeMayorAlcance()
    {
        PerfilAlcance[] todos = [PerfilAlcance.Filial, PerfilAlcance.Tecnologia, PerfilAlcance.Transportacion, PerfilAlcance.Global];

        Assert.Equal(PerfilAlcance.Global, todos.DeMayorAlcance());
    }

    [Fact]
    public void SinPerfilesNoHayGanador() =>
        Assert.Null(Array.Empty<PerfilAlcance>().DeMayorAlcance());

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // Las claves tal como quedan en la base tras la migración del super administrador.
        db.PerfilesPorPosicion.AddRange(
            new PerfilPosicion { Posicion = "SuperAdministrador", Perfil = 1 },
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = 1 },
            new PerfilPosicion { Posicion = "Encargado Transportación", Perfil = 2 },
            new PerfilPosicion { Posicion = "Administrador de Filial", Perfil = 3 },
            new PerfilPosicion { Posicion = "Soporte Técnico", Perfil = 4 },
            new PerfilPosicion { Posicion = "Soporte Tecnico", Perfil = 4 });
        await db.SaveChangesAsync();
        return db;
    }
}

using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// El perfil sale del ROL, y solo del rol. La posición viaja en el token pero ya no concede nada.
///
/// La posición es el cargo de recursos humanos: se escribe de mil formas, no se administra desde
/// AuthManager y, sobre todo, no se puede revocar. Al sacar a alguien de un grupo de seguridad
/// conservaba el alcance porque su cargo se lo seguía dando. El rol sí se quita, y con él el
/// acceso.
/// </summary>
public sealed class PerfilPorRolTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task UnRolMapeadoDaSuPerfilAunqueLaPosicionNoEsteMapeada()
    {
        await using var db = await SembrarAsync(("LogiTrack.Transportacion", PerfilAlcance.Transportacion));
        var usuario = Usuario(posicion: "Encargada de Servicios Generales", roles: ["LogiTrack.Transportacion"]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Transportacion, perfil);
    }

    /// <summary>
    /// El rol decide, aunque la posición esté mapeada a un alcance MAYOR.
    ///
    /// Antes mandaba la posición, y eso hacía imposible revocar: al sacar a alguien de un grupo
    /// conservaba el alcance porque su cargo se lo seguía dando. El cargo es un dato de recursos
    /// humanos que no se administra desde AuthManager; el rol sí.
    /// </summary>
    [Fact]
    public async Task ElRolDecideAunqueLaPosicionEsteMapeadaAUnAlcanceMayor()
    {
        await using var db = await SembrarAsync(
            ("Programador Senior", PerfilAlcance.Global),
            ("LogiTrack.Transportacion", PerfilAlcance.Transportacion));
        var usuario = Usuario(posicion: "Programador Senior", roles: ["LogiTrack.Transportacion"]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Transportacion, perfil);
    }

    [Fact]
    public async Task VariosRolesTomanElPrimeroQueEsteMapeado()
    {
        await using var db = await SembrarAsync(("LogiTrack.Transportacion", PerfilAlcance.Transportacion));
        var usuario = Usuario(posicion: "Sin mapear", roles: ["evaluador", "rrhh-lector", "LogiTrack.Transportacion"]);

        Assert.Equal(PerfilAlcance.Transportacion, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    [Fact]
    public async Task SinPosicionNiRolMapeadoSeCaeAlAlcanceMasRestrictivo()
    {
        await using var db = await SembrarAsync(("LogiTrack.Transportacion", PerfilAlcance.Transportacion));
        var usuario = Usuario(posicion: "Sin mapear", roles: ["otro-rol-cualquiera"]);

        Assert.Equal(PerfilAlcance.Filial, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    private static IUserContext Usuario(string posicion, string[] roles) =>
        new FakeUserContext(UsuarioId, "1,AZUA", roles: roles, position: posicion);

    private static async Task<SistemaEnviosDbContext> SembrarAsync(params (string Clave, PerfilAlcance Perfil)[] filas)
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.AddRange(filas.Select(x => new PerfilPosicion { Posicion = x.Clave, Perfil = (byte)x.Perfil }));
        await db.SaveChangesAsync();
        return db;
    }
}
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// El perfil sale de la posición, pero AuthManager también permite crear un rol dedicado a este
/// sistema, que es más fiable de administrar: la posición es un cargo de recursos humanos y
/// puede escribirse de mil formas, mientras que un rol se asigna a propósito.
///
/// Se aceptan los dos. La posición manda cuando está mapeada, porque es más específica que un
/// rol que agrupa a mucha gente.
/// </summary>
public sealed class PerfilPorRolTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task UnRolMapeadoDaSuPerfilAunqueLaPosicionNoEsteMapeada()
    {
        // Se mapea a Transportación a propósito: con affiliate en el token, el respaldo daría
        // Filial. Si sale Transportación es porque el rol se leyó de verdad.
        await using var db = await SembrarAsync(("LogiTrack.Transportacion", PerfilAlcance.Transportacion));
        var usuario = Usuario(posicion: "Encargada de Servicios Generales", roles: ["LogiTrack.Transportacion"]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Transportacion, perfil);
    }

    [Fact]
    public async Task LaPosicionMandaSobreElRolCuandoLasDosEstanMapeadas()
    {
        await using var db = await SembrarAsync(
            ("Programador Senior", PerfilAlcance.Global),
            ("LogiTrack.Transportacion", PerfilAlcance.Transportacion));
        var usuario = Usuario(posicion: "Programador Senior", roles: ["LogiTrack.Transportacion"]);

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        // La posición es más específica: un rol agrupa a mucha gente, la posición es del cargo.
        Assert.Equal(PerfilAlcance.Global, perfil);
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

        // Tiene filial en el token, así que ve lo suyo y nada más. Nunca Global por descarte.
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

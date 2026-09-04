using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// El alcance Global solo se concede a una posición o un rol mapeados en este sistema. Antes se
/// concedía también por el nombre del rol que trajera el token —"AdministradorGlobal" y
/// parecidos—, así que un administrador de otra aplicación de AuthManager entraba aquí viéndolo
/// todo. Ese nombre lo decide otra aplicación, no nosotros.
/// </summary>
public sealed class AlcanceGlobalSoloMapeadoTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Theory]
    [InlineData("AdministradorGlobal")]
    [InlineData("SuperAdministrador")]
    [InlineData("AdministradorSistema")]
    public async Task UnNombreDeRolAjenoNoConcedeAlcanceGlobal(string rol)
    {
        await using var db = await SembrarAsync();
        // Posición sin mapear a propósito: con una mapeada el respaldo ni se alcanza, y la
        // prueba pasaría sin comprobar nada. Es el escenario real del usuario afectado.
        var usuario = new FakeUserContext(UsuarioId, "25,SAN PEDRO", roles: [rol], position: "Cargo sin mapear");

        var perfil = await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Filial, perfil);
    }

    [Fact]
    public async Task SinPosicionMapeadaTampocoSeAsciendePorElNombreDelRol()
    {
        await using var db = await SembrarAsync();
        var usuario = new FakeUserContext(UsuarioId, "25,SAN PEDRO", roles: ["SuperAdministrador"], position: "Cargo sin mapear");

        // Tiene filial en el token: ve lo suyo. Nunca Global por cómo se llame un rol externo.
        Assert.Equal(PerfilAlcance.Filial, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    [Fact]
    public async Task ElAlcanceGlobalSigueLlegandoPorUnaPosicionMapeada()
    {
        await using var db = await SembrarAsync();
        var usuario = new FakeUserContext(UsuarioId, "30,SEDE", position: "Programador Senior");

        Assert.Equal(PerfilAlcance.Global, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    [Fact]
    public async Task ElAlcanceGlobalTambienLlegaPorUnRolMapeadoAProposito()
    {
        await using var db = await SembrarAsync();
        var usuario = new FakeUserContext(UsuarioId, "30,SEDE", roles: ["LogiTrack.Tecnologia"], position: "Cargo sin mapear");

        Assert.Equal(PerfilAlcance.Global, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.AddRange(
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = (byte)PerfilAlcance.Global },
            new PerfilPosicion { Posicion = "LogiTrack.Tecnologia", Perfil = (byte)PerfilAlcance.Global },
            new PerfilPosicion { Posicion = "Administrador de Filial", Perfil = (byte)PerfilAlcance.Filial });
        await db.SaveChangesAsync();
        return db;
    }
}

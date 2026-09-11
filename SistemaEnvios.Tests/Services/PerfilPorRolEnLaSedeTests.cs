using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El perfil sale del ROL del usuario, no de su filial. En la sede conviven Tecnología,
/// administradores de filial y asistentes administrativos: todos con el mismo affiliate.
/// </summary>
public sealed class PerfilPorRolEnLaSedeTests
{
    private static readonly Guid UsuarioId = Guid.Parse("3b8e1f70-2c4a-4d61-9e05-8a7f6b3c2d19");
    private const string Sede = "30,SANTO DOMINGO (SEDE)";

    [Fact]
    public async Task RolDeTecnologia_ResuelveGlobal()
    {
        await using var db = await CrearContextoAsync(("Programador Senior", PerfilAlcance.Global));

        Assert.Equal(PerfilAlcance.Global, await Perfil(db, "Programador Senior"));
    }

    [Fact]
    public async Task RolDeTransportacion_ResuelveTransportacion()
    {
        await using var db = await CrearContextoAsync(("Encargado de Transportacion", PerfilAlcance.Transportacion));

        Assert.Equal(PerfilAlcance.Transportacion, await Perfil(db, "Encargado de Transportacion"));
    }

    [Fact]
    public async Task RolDeFilial_ResuelveFilial()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PerfilAlcance.Filial));

        Assert.Equal(PerfilAlcance.Filial, await Perfil(db, "Asistente Administrativo"));
    }

    /// <summary>
    /// La prueba que impide la escalada: dos usuarios de la MISMA sede, distinto rol,
    /// deben resolver perfiles distintos. Si el perfil saliera de la ubicación, ambos serían iguales.
    /// </summary>
    [Fact]
    public async Task MismaSede_DistintoRol_ResuelvePerfilesDistintos()
    {
        await using var db = await CrearContextoAsync(
            ("Programador Senior", PerfilAlcance.Global),
            ("Asistente Administrativo", PerfilAlcance.Filial));

        Assert.Equal(PerfilAlcance.Global, await Perfil(db, "Programador Senior"));
        Assert.Equal(PerfilAlcance.Filial, await Perfil(db, "Asistente Administrativo"));
    }

    [Fact]
    public async Task RolSinMapear_ConFilial_CaeEnFilial()
    {
        await using var db = await CrearContextoAsync(("Programador Senior", PerfilAlcance.Global));

        Assert.Equal(PerfilAlcance.Filial, await Perfil(db, "Cargo Desconocido"));
    }

    [Fact]
    public async Task RolSinMapear_SinFilial_NoTieneAlcance()
    {
        await using var db = await CrearContextoAsync();
        var usuario = new FakeUserContext(UsuarioId, affiliate: null, roles: ["Cargo Desconocido"]);

        Assert.Equal(PerfilAlcance.SinAlcance, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    private static Task<PerfilAlcance> Perfil(SistemaEnviosDbContext db, string rol) =>
        AlcanceDePrueba.Crear(db, new FakeUserContext(UsuarioId, Sede, roles: [rol])).ResolverPerfilAsync();

    private static async Task<SistemaEnviosDbContext> CrearContextoAsync(
        params (string Posicion, PerfilAlcance Perfil)[] mapeo)
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.Ubicaciones.Add(new Ubicacion
        {
            Nombre = "Santo Domingo",
            CodigoCentro = "FILIAL-SANTO-DOMINGO",
            FilialExternaId = 30,
            Tipo = TipoUbicacionEnum.Filial,
            Activo = true
        });
        db.PerfilesPorPosicion.AddRange(
            mapeo.Select(x => new PerfilPosicion { Posicion = x.Posicion, Perfil = (byte)x.Perfil }));
        await db.SaveChangesAsync();
        return db;
    }
}

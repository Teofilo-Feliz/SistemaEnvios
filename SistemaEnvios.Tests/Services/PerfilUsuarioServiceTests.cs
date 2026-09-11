using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Seguridad;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El frontend no puede deducir el perfil de los permisos: un administrador de filial y
/// Transportación comparten transportes.gestionar. El backend, que es quien decide, lo expone.
/// </summary>
public sealed class PerfilUsuarioServiceTests
{
    private static readonly Guid UsuarioId = Guid.Parse("9c2b7a41-6f8e-4d30-b512-7ae4c9d80f26");

    [Fact]
    public async Task UsuarioDeFilial_DevuelveSuFilialYUbicacion()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PerfilAlcance.Filial));
        var usuario = new FakeUserContext(
            UsuarioId, "30,SANTO DOMINGO (SEDE)",
            permissions: [PermissionNames.EnviosConsultar],
            roles: ["Asistente Administrativo"]);

        var resultado = await Servicio(db, usuario).ObtenerAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Filial", resultado.Value!.Perfil);
        Assert.Equal(30, resultado.Value.FilialId);
        Assert.Equal("Santo Domingo", resultado.Value.FilialNombre);
        Assert.Contains(PermissionNames.EnviosConsultar, resultado.Value.Permisos);
    }

    [Fact]
    public async Task UsuarioDeTecnologia_DevuelvePerfilGlobal()
    {
        await using var db = await CrearContextoAsync(("Programador Senior", PerfilAlcance.Global));
        var usuario = new FakeUserContext(UsuarioId, "30,SANTO DOMINGO (SEDE)", roles: ["Programador Senior"]);

        var resultado = await Servicio(db, usuario).ObtenerAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Global", resultado.Value!.Perfil);
        Assert.True(resultado.Value.PuedeFiltrarPorFilial);
    }

    [Fact]
    public async Task UsuarioDeFilial_NoPuedeFiltrarPorFilial()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PerfilAlcance.Filial));
        var usuario = new FakeUserContext(UsuarioId, "30,SANTO DOMINGO (SEDE)", roles: ["Asistente Administrativo"]);

        var resultado = await Servicio(db, usuario).ObtenerAsync();

        Assert.False(resultado.Value!.PuedeFiltrarPorFilial);
    }

    [Fact]
    public async Task FilialSinMapear_LoReportaEnLugarDeFingirQueTodoEstaBien()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PerfilAlcance.Filial));
        var usuario = new FakeUserContext(UsuarioId, "77,FILIAL NUEVA", roles: ["Asistente Administrativo"]);

        var resultado = await Servicio(db, usuario).ObtenerAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.FilialNombre);
        Assert.False(resultado.Value.FilialMapeada);
    }

    /// <summary>
    /// El alcance se resuelve con los ROLES, y la respuesta los devuelve junto a la posición.
    /// Sin ellos decía a qué perfil llegó el usuario pero no con qué clave, y averiguarlo obligaba
    /// a leer el log del contenedor o a decodificar el token entero. La posición viaja también,
    /// como dato informativo: ya no concede nada, pero saber el cargo ayuda a entender el caso.
    ///
    /// El caso real: la encargada de soporte técnico llega con una posición que no está registrada
    /// y con dos roles que sí. Viendo las tres claves y el perfil se entiende de un vistazo por qué
    /// aterrizó donde aterrizó.
    /// </summary>
    [Fact]
    public async Task DevuelveLasClavesConLasQueSeResolvioElAlcance()
    {
        await using var db = await CrearContextoAsync(
            ("SuperAdministrador", PerfilAlcance.Global),
            ("Soporte Técnico", PerfilAlcance.Tecnologia));
        var usuario = FakeUserContext.ConRoles(
            UsuarioId, "Encargada de Soporte Técnico", 30, "Soporte Técnico", "SuperAdministrador");

        var resultado = await Servicio(db, usuario).ObtenerAsync();

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal("Encargada de Soporte Técnico", resultado.Value!.Posicion);
        Assert.Equal(["Soporte Técnico", "SuperAdministrador"], resultado.Value.Roles);
        Assert.Equal("Global", resultado.Value.Perfil);
    }

    /// <summary>Un usuario sin roles devuelve la lista vacía, no null.</summary>
    [Fact]
    public async Task SinRolesDevuelveListaVacia()
    {
        await using var db = await CrearContextoAsync(("Asistente Administrativo", PerfilAlcance.Filial));
        var usuario = new FakeUserContext(UsuarioId, "30,SANTO DOMINGO (SEDE)");

        var resultado = await Servicio(db, usuario).ObtenerAsync();

        Assert.NotNull(resultado.Value!.Roles);
        Assert.Empty(resultado.Value.Roles);
    }

    private static PerfilUsuarioService Servicio(SistemaEnviosDbContext db, IUserContext usuario) =>
        new(db, usuario, AlcanceDePrueba.Crear(db, usuario));

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
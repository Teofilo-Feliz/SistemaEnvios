using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Equipos;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El código de activo es único en toda la base (UX_Equipos_CodigoActivo), así que teclear uno que
/// ya pertenece a otro equipo no es una elección: es un error de dato que reventaría al guardar.
///
/// Esta comprobación lo adelanta. Va contra la base entera y no contra el inventario que el
/// formulario tiene cargado, que solo trae los equipos de la ubicación de origen: un código puede
/// pertenecer a un equipo de otra filial y ahí no aparecería.
/// </summary>
public sealed class CodigoActivoDisponibleTests
{
    [Fact]
    public async Task UnCodigoLibreEstaDisponible()
    {
        await using var db = await BaseAsync();

        var resultado = await Servicio(db).CodigoActivoDisponibleAsync("999999");

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.True(resultado.Value);
    }

    [Fact]
    public async Task UnCodigoDeOtroEquipoNoEstaDisponible()
    {
        await using var db = await BaseAsync();

        var resultado = await Servicio(db).CodigoActivoDisponibleAsync("104782");

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.False(resultado.Value);
    }

    /// <summary>
    /// El equipo de otra filial también cuenta. Es el caso que el formulario no puede ver por su
    /// cuenta: su inventario en memoria solo tiene los del origen.
    /// </summary>
    [Fact]
    public async Task UnCodigoDeUnEquipoDeOtraFilialTampoco()
    {
        await using var db = await BaseAsync();

        Assert.False((await Servicio(db).CodigoActivoDisponibleAsync("200500")).Value);
    }

    /// <summary>Editando un equipo, su propio código no choca consigo mismo.</summary>
    [Fact]
    public async Task ElPropioCodigoNoChocaAlExcluirElEquipo()
    {
        await using var db = await BaseAsync();

        Assert.True((await Servicio(db).CodigoActivoDisponibleAsync("104782", excluirEquipoId: 1)).Value);
    }

    /// <summary>
    /// Sin código no hay nada que chocar: la columna admite nulos y el índice único los ignora.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SinCodigoSiempreEstaDisponible(string codigo)
    {
        await using var db = await BaseAsync();

        Assert.True((await Servicio(db).CodigoActivoDisponibleAsync(codigo)).Value);
    }

    private static EquipoService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext usuario = new FakeUserContext(Guid.NewGuid(), "30,SEDE", roles: ["Programador Senior"]);
        return new EquipoService(
            db, new UnitOfWork(db),
            new CrearEquipoRequestValidator(), new ActualizarEquipoRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario));
    }

    private static async Task<SistemaEnviosDbContext> BaseAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Ubicaciones.AddRange(
            new Ubicacion { UbicacionId = 1, Nombre = "Santo Domingo", CodigoCentro = "SD", FilialExternaId = 30, Tipo = TipoUbicacionEnum.Filial, Activo = true },
            new Ubicacion { UbicacionId = 2, Nombre = "Azua", CodigoCentro = "AZ", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true });

        db.Equipos.AddRange(
            new Equipo { EquipoId = 1, CodigoActivo = "104782", NumeroSerie = "MXL04916TL", Marca = "HP", Modelo = "Compaq", TipoEquipoId = 1, UbicacionActualId = 1 },
            new Equipo { EquipoId = 2, CodigoActivo = "200500", NumeroSerie = "OTRO-SERIAL", Marca = "Dell", Modelo = "OptiPlex", TipoEquipoId = 1, UbicacionActualId = 2 });

        await db.SaveChangesAsync();
        return db;
    }
}

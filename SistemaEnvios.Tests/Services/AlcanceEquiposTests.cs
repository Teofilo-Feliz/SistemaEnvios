using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Equipos;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El inventario de equipos tambien se acota. Un usuario de filial no debe listar ni modificar
/// equipos de otras filiales: mover un equipo ajeno de ubicacion desincroniza el registro de
/// la realidad fisica.
/// </summary>
public sealed class AlcanceEquiposTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8e4d2c19-6a35-4f70-b1c8-3d9f0a7e5b26");
    private const string PosicionFilial = "Asistente Administrativo";
    private const string PosicionGlobal = "Programador Senior";
    private const string ClaimSantiago = "41,SANTIAGO";

    [Fact]
    public async Task UsuarioDeFilial_SoloListaLosEquiposDeSuFilial()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db, PosicionFilial).ListarAsync(new ConsultarEquiposRequest());

        Assert.True(resultado.IsSuccess);
        var equipo = Assert.Single(resultado.Value!.Items);
        Assert.Equal("SN-SANTIAGO", equipo.NumeroSerie);
    }

    [Fact]
    public async Task PerfilGlobal_ListaTodoElInventario()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db, PosicionGlobal).ListarAsync(new ConsultarEquiposRequest());

        Assert.Equal(3, resultado.Value!.Items.Count);
    }

    [Fact]
    public async Task UsuarioDeFilial_NoAccedeAUnEquipoAjeno()
    {
        await using var db = await SembrarAsync();
        var ajeno = await db.Equipos.FirstAsync(x => x.NumeroSerie == "SN-AZUA");

        var resultado = await Servicio(db, PosicionFilial).ObtenerAsync(ajeno.EquipoId);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task UsuarioDeFilial_NoPuedeMoverUnEquipoAjeno()
    {
        await using var db = await SembrarAsync();
        var ajeno = await db.Equipos.Include(x => x.TipoEquipo).FirstAsync(x => x.NumeroSerie == "SN-AZUA");
        var suUbicacion = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 41);

        var resultado = await Servicio(db, PosicionFilial).ActualizarAsync(new ActualizarEquipoRequest
        {
            EquipoId = ajeno.EquipoId,
            TipoEquipoId = ajeno.TipoEquipoId,
            UbicacionActualId = suUbicacion.UbicacionId,
            Marca = ajeno.Marca,
            Modelo = ajeno.Modelo,
            NumeroSerie = ajeno.NumeroSerie,
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task UsuarioDeFilial_SiPuedeVerYEditarElSuyo()
    {
        await using var db = await SembrarAsync();
        var propio = await db.Equipos.FirstAsync(x => x.NumeroSerie == "SN-SANTIAGO");

        var visto = await Servicio(db, PosicionFilial).ObtenerAsync(propio.EquipoId);

        Assert.True(visto.IsSuccess, visto.Error);
    }

    [Fact]
    public async Task UsuarioDeFilial_NoRegistraEquiposEnOtraUbicacion()
    {
        await using var db = await SembrarAsync();
        var ajena = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 1);
        var tipo = await db.TiposEquipo.FirstAsync();

        var resultado = await Servicio(db, PosicionFilial).CrearAsync(new CrearEquipoRequest
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = ajena.UbicacionId,
            Marca = "Dell",
            Modelo = "Latitude",
            NumeroSerie = "SN-NUEVO",
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    private static EquipoService Servicio(SistemaEnviosDbContext db, string posicion)
    {
        IUserContext usuario = new FakeUserContext(UsuarioId, ClaimSantiago, position: posicion);
        return new EquipoService(
            db, new UnitOfWork(db),
            new CrearEquipoRequestValidator(), new ActualizarEquipoRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "FIL-A", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "FIL-B", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(santiago, azua, tecnologia, tipo);
        db.AddRange(
            new PerfilPosicion { Posicion = PosicionFilial, Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = PosicionGlobal, Perfil = (byte)PerfilAlcance.Global });
        db.EstadosEnvio.Add(new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true });
        await db.SaveChangesAsync();

        db.Equipos.AddRange(
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = santiago.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-SANTIAGO" },
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Dell", Modelo = "L2", NumeroSerie = "SN-AZUA" },
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = tecnologia.UbicacionId, Marca = "Dell", Modelo = "L3", NumeroSerie = "SN-TEC" });
        await db.SaveChangesAsync();
        return db;
    }
}

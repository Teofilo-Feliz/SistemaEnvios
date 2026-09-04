using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Equipos;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El inventario es la tabla que más crece: 34 filiales por todos sus equipos. Paginarla no es
/// cosmético, es lo que evita que cada apertura de la pantalla arrastre todo el inventario.
/// </summary>
public sealed class PaginacionEquiposTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8e4d2c19-6a35-4f70-b1c8-3d9f0a7e5b26");

    [Fact]
    public async Task DevuelveSoloLaPaginaPedidaAunqueElInventarioSeaGrande()
    {
        await using var db = await SembrarAsync(120);

        var resultado = await Servicio(db).ListarAsync(new ConsultarEquiposRequest { Page = 1, PageSize = 20 });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(20, resultado.Value!.Items.Count);
        Assert.Equal(120, resultado.Value.TotalItems);
        Assert.Equal(6, resultado.Value.TotalPages);
    }

    [Fact]
    public async Task PedirUnTamanoDesmedidoNoDevuelveElInventarioCompleto()
    {
        await using var db = await SembrarAsync(120);

        var resultado = await Servicio(db).ListarAsync(new ConsultarEquiposRequest { PageSize = 100_000 });

        Assert.Equal(ParametrosPagina.TamanoMaximo, resultado.Value!.Items.Count);
    }

    [Fact]
    public async Task ElTotalRespetaElAlcanceDeLaFilialYNoCuentaLoAjeno()
    {
        await using var db = await SembrarAsync(120);

        var resultado = await Servicio(db, "Asistente Administrativo").ListarAsync(new ConsultarEquiposRequest());

        // Solo los pares quedaron en Santiago; el resto es de Azua.
        Assert.Equal(60, resultado.Value!.TotalItems);
        Assert.All(resultado.Value.Items, x => Assert.EndsWith("-SANTIAGO", x.NumeroSerie));
    }

    [Fact]
    public async Task FiltraPorUbicacionYPorTextoDentroDeLaConsulta()
    {
        await using var db = await SembrarAsync(120);
        var azua = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 1);

        var porUbicacion = await Servicio(db).ListarAsync(new ConsultarEquiposRequest { UbicacionActualId = azua.UbicacionId });
        Assert.Equal(60, porUbicacion.Value!.TotalItems);

        var porTexto = await Servicio(db).ListarAsync(new ConsultarEquiposRequest { Search = "SN-007-" });
        Assert.Equal(1, porTexto.Value!.TotalItems);
    }

    private static EquipoService Servicio(SistemaEnviosDbContext db, string posicion = "Programador Senior")
    {
        IUserContext usuario = new FakeUserContext(UsuarioId, "41,SANTIAGO", position: posicion);
        return new EquipoService(
            db, new UnitOfWork(db),
            new CrearEquipoRequestValidator(), new ActualizarEquipoRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync(int cantidad)
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "FIL-A", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "FIL-B", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(santiago, azua, tipo);
        db.AddRange(
            new PerfilPosicion { Posicion = "Asistente Administrativo", Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = (byte)PerfilAlcance.Global });
        await db.SaveChangesAsync();

        db.Equipos.AddRange(Enumerable.Range(1, cantidad).Select(i => new Equipo
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = i % 2 == 0 ? santiago.UbicacionId : azua.UbicacionId,
            Marca = "Dell",
            Modelo = $"L{i:D3}",
            NumeroSerie = $"SN-{i:D3}-{(i % 2 == 0 ? "SANTIAGO" : "AZUA")}"
        }));
        await db.SaveChangesAsync();
        return db;
    }
}

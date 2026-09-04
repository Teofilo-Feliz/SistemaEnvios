using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El ticket pertenece al caso, no al viaje: mientras el equipo tenga un caso abierto, todo
/// movimiento hereda su ticket y el usuario no lo escribe. Solo un equipo sin caso abierto —una
/// asignación inicial o un equipo ya devuelto— puede estrenar uno.
/// </summary>
public sealed class HerenciaTicketTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task UnEquipoSinCasoAbiertoEstrenaElTicketQueSeEscribe()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);

        var resultado = await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.Equipo, "145"));

        Assert.True(resultado.IsSuccess, resultado.Error);
        var fila = await db.EnvioEquipos.SingleAsync();
        Assert.Equal("145", fila.NumeroTicket);
        Assert.Null(fila.EnvioEquipoOrigenId);
    }

    [Fact]
    public async Task LaDevolucionHeredaElTicketAunqueSeEnvieOtroDistinto()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);
        await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.Equipo, "145"));
        await MoverEquipoAsync(db, e.Equipo, e.Tecnologia);

        // El usuario manda 999; el sistema debe ignorarlo y poner el ticket del caso.
        var resultado = await Servicio(db).CrearConEquiposAsync(Solicitud(e.Tecnologia, e.Filial, e.Equipo, "999"));

        Assert.True(resultado.IsSuccess, resultado.Error);
        var devolucion = await db.EnvioEquipos.OrderByDescending(x => x.EnvioEquipoId).FirstAsync();
        Assert.Equal("145", devolucion.NumeroTicket);
    }

    [Fact]
    public async Task LaContinuacionQuedaEncadenadaAlMovimientoQueAbrioElCaso()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);
        await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.Equipo, "145"));
        var apertura = await db.EnvioEquipos.SingleAsync();
        await MoverEquipoAsync(db, e.Equipo, e.Tecnologia);

        await Servicio(db).CrearConEquiposAsync(Solicitud(e.Tecnologia, e.Filial, e.Equipo, "999"));

        var devolucion = await db.EnvioEquipos.OrderByDescending(x => x.EnvioEquipoId).FirstAsync();
        Assert.Equal(apertura.EnvioEquipoId, devolucion.EnvioEquipoOrigenId);
    }

    [Fact]
    public async Task LaDevolucionNoPuedeIrAUnaFilialDistintaDeLaQueAbrioElCaso()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);
        await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.Equipo, "145"));
        await MoverEquipoAsync(db, e.Equipo, e.Tecnologia);

        var resultado = await Servicio(db).CrearConEquiposAsync(Solicitud(e.Tecnologia, e.OtraFilial, e.Equipo, "145"));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    [Fact]
    public async Task TrasDescartarloElEquipoPuedeIrAOtraFilialConTicketNuevo()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);
        await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.Equipo, "145"));
        await MoverEquipoAsync(db, e.Equipo, e.Tecnologia);
        await LiberarReservasAsync(db);
        Assert.True((await Caso(db).DescartarAsync(e.Equipo, "Reasignado a otra filial.")).IsSuccess);

        var resultado = await Servicio(db).CrearConEquiposAsync(Solicitud(e.Tecnologia, e.OtraFilial, e.Equipo, "500"));

        Assert.True(resultado.IsSuccess, resultado.Error);
        var envio = await db.EnvioEquipos.OrderByDescending(x => x.EnvioEquipoId).FirstAsync();
        Assert.Equal("500", envio.NumeroTicket);
        Assert.Null(envio.EnvioEquipoOrigenId);
    }

    [Fact]
    public async Task UnTicketYaUsadoParaAbrirOtroCasoSeRechaza()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);
        await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.Equipo, "145"));

        var resultado = await Servicio(db).CrearConEquiposAsync(Solicitud(e.Filial, e.Tecnologia, e.OtroEquipo, "145"));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    // ---------- apoyo ----------

    private sealed record Contexto(int Filial, int OtraFilial, int Tecnologia, int Equipo, int OtroEquipo);

    private static Contexto Datos(SistemaEnviosDbContext db) => new(
        db.Ubicaciones.First(x => x.CodigoCentro == "F09").UbicacionId,
        db.Ubicaciones.First(x => x.CodigoCentro == "F01").UbicacionId,
        db.Ubicaciones.First(x => x.Tipo == TipoUbicacionEnum.Tecnologia).UbicacionId,
        db.Equipos.First(x => x.NumeroSerie == "SN-BANI").EquipoId,
        db.Equipos.First(x => x.NumeroSerie == "SN-OTRO").EquipoId);

    private static CrearEnvioConEquiposRequest Solicitud(int origen, int destino, int equipoId, string ticket) => new()
    {
        UbicacionOrigenId = origen,
        UbicacionDestinoId = destino,
        Observaciones = "Envío de prueba",
        Equipos = [new EquipoEnvioInicialRequest { EquipoId = equipoId, NumeroTicket = ticket, Observaciones = "Equipo" }]
    };

    /// <summary>Simula que el envío llegó: el equipo cambia de sitio y se libera su reserva.</summary>
    private static async Task MoverEquipoAsync(SistemaEnviosDbContext db, int equipoId, int ubicacionId)
    {
        var equipo = await db.Equipos.FirstAsync(x => x.EquipoId == equipoId);
        equipo.UbicacionActualId = ubicacionId;
        await LiberarReservasAsync(db);
    }

    private static async Task LiberarReservasAsync(SistemaEnviosDbContext db)
    {
        db.ReservasEquipoEnvio.RemoveRange(db.ReservasEquipoEnvio);
        await db.SaveChangesAsync();
    }

    private static CasoEquipoService Caso(SistemaEnviosDbContext db)
    {
        var u = FakeUserContext.Global(UsuarioId);
        return new CasoEquipoService(db, new UnitOfWork(db), u, AlcanceDePrueba.Crear(db, u));
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var u = FakeUserContext.Global(UsuarioId);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            u, AlcanceDePrueba.Crear(db, u), Caso(db));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var bani = new Ubicacion { Nombre = "Baní", CodigoCentro = "F09", FilialExternaId = 9, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(bani, azua, tecnologia, tipo);
        db.EstadosEnvio.AddRange(
            new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true },
            new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnPreparacionTecnologia, Nombre = "En preparación", Activo = true });
        await db.SaveChangesAsync();

        db.Equipos.AddRange(
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = bani.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-BANI" },
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = bani.UbicacionId, Marca = "Dell", Modelo = "L2", NumeroSerie = "SN-OTRO" });
        await db.SaveChangesAsync();
        return db;
    }
}

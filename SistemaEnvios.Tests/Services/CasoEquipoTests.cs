using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Un caso está abierto desde que el equipo sale de su filial hasta que vuelve y esa misma
/// filial lo recibe conforme. Mientras siga abierto, cada movimiento del equipo hereda su
/// ticket; solo un equipo sin caso abierto puede estrenar uno.
///
/// El cierre se sella en la fila de apertura cuando ocurre, en vez de recalcularse: preguntar
/// "¿tiene caso abierto?" no debe depender de reconstruir la historia del equipo.
/// </summary>
public sealed class CasoEquipoTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task UnEquipoQueNuncaSalioNoTieneCasoAbierto()
    {
        await using var db = await SembrarAsync();

        var caso = await Servicio(db).BuscarAbiertoAsync(Equipo(db));

        Assert.Null(caso);
    }

    [Fact]
    public async Task SalirDeLaFilialAbreElCasoConSuTicketYSuFilial()
    {
        await using var db = await SembrarAsync();
        await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");

        var caso = await Servicio(db).BuscarAbiertoAsync(Equipo(db));

        Assert.NotNull(caso);
        Assert.Equal("145", caso!.NumeroTicket);
        Assert.Equal(FilialId(db), caso.FilialId);
    }

    [Fact]
    public async Task ElCasoSigueAbiertoMientrasElEquipoEsteEnTecnologia()
    {
        await using var db = await SembrarAsync();
        var apertura = await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");
        // La devolución ya salió, pero el equipo todavía no llegó a la filial.
        await MoverAsync(db, DireccionEnvioEnum.HaciaFilial, "145", apertura.EnvioEquipoId);

        Assert.NotNull(await Servicio(db).BuscarAbiertoAsync(Equipo(db)));
    }

    [Fact]
    public async Task RecibirConformeEnLaFilialCierraElCaso()
    {
        await using var db = await SembrarAsync();
        var apertura = await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");
        await MoverAsync(db, DireccionEnvioEnum.HaciaFilial, "145", apertura.EnvioEquipoId);

        await Servicio(db).CerrarAsync(apertura.EnvioEquipoId, "Recibido conforme en la filial.", UsuarioId);

        Assert.Null(await Servicio(db).BuscarAbiertoAsync(Equipo(db)));
    }

    [Fact]
    public async Task UnEquipoConCasoCerradoVuelveAEstrenarTicket()
    {
        await using var db = await SembrarAsync();
        var apertura = await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");
        await Servicio(db).CerrarAsync(apertura.EnvioEquipoId, "Recibido conforme en la filial.", UsuarioId);

        var caso = await Servicio(db).BuscarAbiertoAsync(Equipo(db));

        Assert.Null(caso);
    }

    [Fact]
    public async Task LasVueltasDelCasoSeCuentanParaQueUnCasoQueNoAvanzaSeVea()
    {
        await using var db = await SembrarAsync();
        var apertura = await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");
        await MoverAsync(db, DireccionEnvioEnum.HaciaFilial, "145", apertura.EnvioEquipoId);
        await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145", apertura.EnvioEquipoId);

        var caso = await Servicio(db).BuscarAbiertoAsync(Equipo(db));

        Assert.Equal(3, caso!.Movimientos);
    }

    [Fact]
    public async Task CerrarDejaConstanciaDelMotivo()
    {
        await using var db = await SembrarAsync();
        var apertura = await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");

        await Servicio(db).CerrarAsync(apertura.EnvioEquipoId, "Descartado: equipo dado de baja.", UsuarioId);

        var fila = await db.EnvioEquipos.FirstAsync(x => x.EnvioEquipoId == apertura.EnvioEquipoId);
        Assert.NotNull(fila.FechaCierreCaso);
        Assert.Equal("Descartado: equipo dado de baja.", fila.MotivoCierreCaso);
    }

    [Fact]
    public async Task CerrarDosVecesNoPisaElPrimerCierre()
    {
        await using var db = await SembrarAsync();
        var apertura = await MoverAsync(db, DireccionEnvioEnum.HaciaTecnologia, "145");
        var servicio = Servicio(db);
        await servicio.CerrarAsync(apertura.EnvioEquipoId, "Recibido conforme en la filial.", UsuarioId);

        await servicio.CerrarAsync(apertura.EnvioEquipoId, "Descartado por error.", UsuarioId);

        var fila = await db.EnvioEquipos.FirstAsync(x => x.EnvioEquipoId == apertura.EnvioEquipoId);
        Assert.Equal("Recibido conforme en la filial.", fila.MotivoCierreCaso);
    }

    // ---------- apoyo ----------

    private static CasoEquipoService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext u = FakeUserContext.Global(UsuarioId);
        return new CasoEquipoService(db, new UnitOfWork(db), u, new AlcanceEnvios(db, u));
    }

    private static int Equipo(SistemaEnviosDbContext db) => db.Equipos.First().EquipoId;
    private static int FilialId(SistemaEnviosDbContext db) =>
        db.Ubicaciones.First(x => x.Tipo == TipoUbicacionEnum.Filial).UbicacionId;

    /// <summary>Crea un envío con el equipo dentro, como continuación si se indica el origen.</summary>
    private static async Task<EnvioEquipo> MoverAsync(
        SistemaEnviosDbContext db, DireccionEnvioEnum direccion, string ticket, int? origenId = null)
    {
        var filial = db.Ubicaciones.First(x => x.Tipo == TipoUbicacionEnum.Filial);
        var tecnologia = db.Ubicaciones.First(x => x.Tipo == TipoUbicacionEnum.Tecnologia);
        var estado = db.EstadosEnvio.First();
        var envio = new Envio
        {
            NumeroEnvio = $"ENV-{Guid.NewGuid():N}"[..17],
            UbicacionOrigenId = direccion == DireccionEnvioEnum.HaciaTecnologia ? filial.UbicacionId : tecnologia.UbicacionId,
            UbicacionDestinoId = direccion == DireccionEnvioEnum.HaciaTecnologia ? tecnologia.UbicacionId : filial.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        var fila = new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = db.Equipos.First().EquipoId,
            NumeroTicket = ticket,
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Movimiento de prueba",
            EnvioEquipoOrigenId = origenId
        };
        db.EnvioEquipos.Add(fila);
        await db.SaveChangesAsync();
        return fila;
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Baní", CodigoCentro = "F09", FilialExternaId = 9, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(filial, tecnologia, tipo);
        db.EstadosEnvio.Add(new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true });
        await db.SaveChangesAsync();

        db.Equipos.Add(new Equipo
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = filial.UbicacionId,
            Marca = "Dell",
            Modelo = "L1",
            NumeroSerie = "SN-BANI"
        });
        await db.SaveChangesAsync();
        return db;
    }
}

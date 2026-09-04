using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Infrastructure.Services.Transportes;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Flujo de Tecnología hacia filial, ya simplificado:
///   PREPARACION_TECNOLOGIA -> DESPACHADO_TECNOLOGIA -> EN_TRANSPORTACION
///   -> EN_TRANSITO (al asignar chofer) -> RECIBIDO_FILIAL (con o sin incidencia).
/// Desaparecen TRANSPORTE_ASIGNADO, DESPACHADO_TRANSPORTACION, PENDIENTE_RECEPCION_FILIAL y
/// RECEPCION_VALIDADA_FILIAL: eran cuatro pasos que no representaban ninguna decisión real.
/// </summary>
public sealed class FlujoHaciaFilialTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task TecnologiaConfirmaLaEntregaYElEnvioQuedaEnTransportacion()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnPreparacionTecnologia);

        var resultado = await Estados(db).DespacharDesdeTecnologiaAsync(e.Envio.EnvioId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(e.Estados[EstadoEnvioCodigos.EnTransportacion].EstadoEnvioId, e.Envio.EstadoEnvioId);
    }

    [Fact]
    public async Task AsignarChoferPoneElEnvioEnTransitoDeInmediato()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransportacion);

        var resultado = await Transportes(db).CrearAsync(new CrearTransporteRequest
        {
            EnvioId = e.Envio.EnvioId,
            TipoTransporteId = e.Tipo.TipoTransporteId,
            ChoferInternoId = e.Chofer.ChoferInternoId,
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(e.Estados[EstadoEnvioCodigos.EnTransito].EstadoEnvioId, e.Envio.EstadoEnvioId);
    }

    [Fact]
    public async Task AsignarChoferNoDejaPasoIntermedioDeChoferAsignado()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransportacion);

        await Transportes(db).CrearAsync(new CrearTransporteRequest
        {
            EnvioId = e.Envio.EnvioId,
            TipoTransporteId = e.Tipo.TipoTransporteId,
            ChoferInternoId = e.Chofer.ChoferInternoId,
        });

        var recorrido = await db.HistorialEstadosEnvio
            .Where(x => x.EnvioId == e.Envio.EnvioId)
            .OrderBy(x => x.HistorialEstadoEnvioId)
            .Select(x => x.EstadoEnvioId).ToListAsync();
        Assert.Equal([e.Estados[EstadoEnvioCodigos.EnTransito].EstadoEnvioId], recorrido);
    }

    [Fact]
    public async Task AsignarChoferNoGeneraAvisos()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransportacion);

        await Transportes(db).CrearAsync(new CrearTransporteRequest
        {
            EnvioId = e.Envio.EnvioId,
            TipoTransporteId = e.Tipo.TipoTransporteId,
            ChoferInternoId = e.Chofer.ChoferInternoId,
        });

        // El único aviso del sistema es el de llegada a Tecnología; asignar chofer no avisa.
        Assert.Empty(await db.Notificaciones.Where(x => x.EnvioId == e.Envio.EnvioId).ToListAsync());
    }

    [Fact]
    public async Task LaFilialRecibeDirectamenteDesdeEnTransito()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransito);

        var resultado = await Recepciones(db).CrearAsync(new CrearRecepcionRequest { EnvioId = e.Envio.EnvioId });

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task LaRecepcionEnFilialTerminaEnRecibidoFilial()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransito);
        var servicio = Recepciones(db);
        var recepcionId = (await servicio.CrearAsync(new CrearRecepcionRequest { EnvioId = e.Envio.EnvioId })).Value;
        await servicio.VerificarEquipoAsync(new VerificarEquipoRequest
        {
            RecepcionId = recepcionId,
            EnvioEquipoId = e.EnvioEquipoId,
            Estado = EstadoRecepcionEquipoEnum.Verificado,
        });

        var resultado = await servicio.CompletarAsync(recepcionId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(e.Estados[EstadoEnvioCodigos.RecibidoEnFilial].EstadoEnvioId, e.Envio.EstadoEnvioId);
    }

    [Fact]
    public async Task LaRecepcionConIncidenciaTerminaEnSuPropioEstado()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransito);
        var servicio = Recepciones(db);
        var recepcionId = (await servicio.CrearAsync(new CrearRecepcionRequest { EnvioId = e.Envio.EnvioId })).Value;
        await servicio.VerificarEquipoAsync(new VerificarEquipoRequest
        {
            RecepcionId = recepcionId,
            EnvioEquipoId = e.EnvioEquipoId,
            Estado = EstadoRecepcionEquipoEnum.VerificadoConIncidencia,
            Observaciones = "Llegó con la pantalla rota.",
        });

        var resultado = await servicio.CompletarAsync(recepcionId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        // El estado lo dice: quien mira la lista no debe abrir la recepción para enterarse.
        Assert.Equal(e.Estados[EstadoEnvioCodigos.RecibidoEnFilialConIncidencia].EstadoEnvioId, e.Envio.EstadoEnvioId);
        var recepcion = await db.Recepciones.FirstAsync(x => x.RecepcionId == recepcionId);
        Assert.Equal(EstadoRecepcionEnum.CompletadaConIncidencia, recepcion.EstadoRecepcion);
    }

    [Fact]
    public async Task UnEnvioYaEnRutaNoAdmiteAsignarleTransporte()
    {
        await using var db = CrearContexto();
        var e = await SembrarAsync(db, EstadoEnvioCodigos.EnTransito);

        var resultado = await Transportes(db).CrearAsync(new CrearTransporteRequest
        {
            EnvioId = e.Envio.EnvioId,
            TipoTransporteId = e.Tipo.TipoTransporteId,
            ChoferInternoId = e.Chofer.ChoferInternoId,
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    private sealed record Escenario(
        Envio Envio, TipoTransporte Tipo, ChoferInterno Chofer,
        Dictionary<string, EstadoEnvio> Estados, int EnvioEquipoId);

    private static async Task<Escenario> SembrarAsync(SistemaEnviosDbContext db, string codigoInicial)
    {
        // El flujo nuevo completo, tal como debe quedar la tabla de transiciones.
        string[] codigos = [
            EstadoEnvioCodigos.EnPreparacionTecnologia, EstadoEnvioCodigos.DespachadoPorTecnologia,
            EstadoEnvioCodigos.EnTransportacion, EstadoEnvioCodigos.EnTransito,
            EstadoEnvioCodigos.RecibidoEnFilial, EstadoEnvioCodigos.RecibidoEnFilialConIncidencia];
        var estados = codigos.ToDictionary(c => c, c => new EstadoEnvio
        {
            Codigo = c,
            Nombre = c,
            Activo = true,
            EsFinal = c is EstadoEnvioCodigos.RecibidoEnFilial or EstadoEnvioCodigos.RecibidoEnFilialConIncidencia
        });
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var filial = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tipo = new TipoTransporte { Codigo = "INTERNO", Nombre = "Interno", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        var chofer = new ChoferInterno { NombreCompleto = "Chofer prueba", NumeroEmpleado = "EMP-1", Activo = true };
        var tipoEquipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(estados.Values);
        db.AddRange(tecnologia, filial, tipo, chofer, tipoEquipo);
        await db.SaveChangesAsync();

        (string De, string A)[] pasos = [
            (EstadoEnvioCodigos.EnPreparacionTecnologia, EstadoEnvioCodigos.DespachadoPorTecnologia),
            (EstadoEnvioCodigos.DespachadoPorTecnologia, EstadoEnvioCodigos.EnTransportacion),
            (EstadoEnvioCodigos.EnTransportacion, EstadoEnvioCodigos.EnTransito),
            (EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.RecibidoEnFilial),
            (EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.RecibidoEnFilialConIncidencia)];
        db.TransicionesEstadoEnvio.AddRange(pasos.Select(p => new TransicionEstadoEnvio
        {
            EstadoOrigenId = estados[p.De].EstadoEnvioId,
            EstadoDestinoId = estados[p.A].EstadoEnvioId,
            Activo = true
        }));

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-FILIAL",
            UbicacionOrigenId = tecnologia.UbicacionId,
            UbicacionDestinoId = filial.UbicacionId,
            EstadoEnvioId = estados[codigoInicial].EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaFilial,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        var equipo = new Equipo { TipoEquipoId = tipoEquipo.TipoEquipoId, UbicacionActualId = tecnologia.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-1" };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();
        var envioEquipo = new EnvioEquipo { EnvioId = envio.EnvioId, EquipoId = equipo.EquipoId, NumeroTicket = "1001", UsuarioSolicitanteId = UsuarioId, Observaciones = "Equipo de prueba" };
        db.EnvioEquipos.Add(envioEquipo);
        await db.SaveChangesAsync();

        return new Escenario(envio, tipo, chofer, estados, envioEquipo.EnvioEquipoId);
    }

    private static TransporteService Transportes(SistemaEnviosDbContext db)
    {
        var u = FakeUserContext.Global(UsuarioId);
        return new TransporteService(db, new UnitOfWork(db),
            new CrearTransporteRequestValidator(), new ActualizarTransporteRequestValidator(),
            u, AlcanceDePrueba.Crear(db, u));
    }

    private static EstadoEnvioService Estados(SistemaEnviosDbContext db)
    {
        var u = FakeUserContext.Global(UsuarioId);
        return new EstadoEnvioService(db, new UnitOfWork(db), u, AlcanceDePrueba.Crear(db, u));
    }

    private static RecepcionService Recepciones(SistemaEnviosDbContext db)
    {
        var u = FakeUserContext.Global(UsuarioId);
        return new RecepcionService(db, new UnitOfWork(db),
            new CrearRecepcionRequestValidator(), new VerificarEquipoRequestValidator(),
            new AsignarTecnicoRequestValidator(), u, AlcanceDePrueba.Crear(db, u),
            new CasoEquipoService(db, new UnitOfWork(db), u, AlcanceDePrueba.Crear(db, u)));
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}

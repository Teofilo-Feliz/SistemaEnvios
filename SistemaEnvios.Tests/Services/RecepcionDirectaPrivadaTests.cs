using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El envío por transporte privado va de la filial directo a Tecnología y no pasa por
/// Transportación, así que nadie confirma su llegada por él: cuando Tecnología lo tiene delante,
/// lo recibe. Antes tenía que recorrer ESPERA_TECNOLOGIA y EN_REVISION —tres pantallas— para lo
/// que en el institucional es un solo paso, y el paso intermedio escribía en el historial
/// "Equipos recibidos y verificados por Tecnología" sin que nadie hubiera verificado nada.
///
/// Lo que abre esa puerta es la ESTRATEGIA, no el estado: un envío institucional en EN_TRANSITO
/// sigue en la carretera y es de Transportación. Esa distinción es lo que más se prueba aquí.
/// </summary>
public sealed class RecepcionDirectaPrivadaTests
{
    private static readonly Guid UsuarioId = Guid.Parse("b17e5d92-3c48-4a61-9f07-52ac8e6d1b34");

    [Fact]
    public async Task PrivadoEnTransitoSeRecibeConformeEnUnSoloPaso()
    {
        await using var db = await SembrarAsync(EstrategiaTransporteEnum.EntregaDirectaTecnologia, EstadoEnvioCodigos.EnTransito);

        var resultado = await RecibirAsync(db, EstadoRecepcionEquipoEnum.Verificado);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(EstadoEnvioCodigos.RecibidoPorTecnologia, await EstadoFinalAsync(db));
    }

    [Fact]
    public async Task PrivadoEnTransitoSeRecibeConIncidencia()
    {
        await using var db = await SembrarAsync(EstrategiaTransporteEnum.EntregaDirectaTecnologia, EstadoEnvioCodigos.EnTransito);

        var resultado = await RecibirAsync(db, EstadoRecepcionEquipoEnum.VerificadoConIncidencia);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(EstadoEnvioCodigos.RecibidoPorTecnologiaConIncidencia, await EstadoFinalAsync(db));
    }

    /// <summary>
    /// El guardia que impide que abrir la transición EN_TRANSITO -> RECIBIDO_TECNOLOGIA se
    /// convierta en un agujero: ese envío va en un camión de Transportación y no ha llegado.
    /// </summary>
    [Fact]
    public async Task InstitucionalEnTransitoNoSePuedeRecibir()
    {
        await using var db = await SembrarAsync(EstrategiaTransporteEnum.TransportacionInstitucional, EstadoEnvioCodigos.EnTransito);

        var resultado = await RecibirAsync(db, EstadoRecepcionEquipoEnum.Verificado);

        Assert.True(resultado.IsFailure);
        Assert.Contains("no permite iniciar la recepción", resultado.Error);
        Assert.Equal(EstadoEnvioCodigos.EnTransito, await EstadoFinalAsync(db));
    }

    /// <summary>El camino del institucional no cambia: se recibe tras la llegada.</summary>
    [Fact]
    public async Task InstitucionalSigueRecibiendoseDesdeRecibidoPorTransportacion()
    {
        await using var db = await SembrarAsync(EstrategiaTransporteEnum.TransportacionInstitucional, EstadoEnvioCodigos.RecibidoPorTransportacion);

        var resultado = await RecibirAsync(db, EstadoRecepcionEquipoEnum.Verificado);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(EstadoEnvioCodigos.RecibidoPorTecnologia, await EstadoFinalAsync(db));
    }

    // ---------- apoyo ----------

    private static async Task<string> EstadoFinalAsync(SistemaEnviosDbContext db)
    {
        var envio = await db.Envios.Include(x => x.EstadoEnvio).FirstAsync();
        return envio.EstadoEnvio.Codigo;
    }

    private static async Task<Application.Common.Result> RecibirAsync(
        SistemaEnviosDbContext db, EstadoRecepcionEquipoEnum estado)
    {
        var servicio = Servicio(db);
        var envioId = db.Envios.First().EnvioId;
        var envioEquipoId = db.EnvioEquipos.First().EnvioEquipoId;

        // El primer guardia está al crear la recepción. Si rechaza, se devuelve ESE motivo: pasar
        // un id vacío al siguiente paso solo produciría un error de validación que esconde la
        // causa real.
        var creada = await servicio.CrearAsync(new CrearRecepcionRequest { EnvioId = envioId });
        if (creada.IsFailure) return Application.Common.Result.Failure(creada.Error!, creada.ErrorType);

        var verificado = await servicio.VerificarEquipoAsync(new VerificarEquipoRequest
        {
            RecepcionId = creada.Value,
            EnvioEquipoId = envioEquipoId,
            Estado = estado,
            Observaciones = estado == EstadoRecepcionEquipoEnum.VerificadoConIncidencia ? "Llegó con el teclado suelto." : null,
        });
        return verificado.IsFailure ? verificado : await servicio.CompletarAsync(creada.Value);
    }

    private static RecepcionService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext u = FakeUserContext.Global(UsuarioId);
        var alcance = AlcanceDePrueba.Crear(db, u);
        return new RecepcionService(db, new UnitOfWork(db),
            new CrearRecepcionRequestValidator(), new VerificarEquipoRequestValidator(),
            new AsignarTecnicoRequestValidator(), u, alcance,
            new CasoEquipoService(db, new UnitOfWork(db), u, alcance));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync(
        EstrategiaTransporteEnum estrategia, string codigoEstadoInicial)
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Baní", CodigoCentro = "BANI", FilialExternaId = 2, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var recibidoTransportacion = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTransportacion, Nombre = "Recibido por Transportación", Activo = true };
        var recibido = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTecnologia, Nombre = "Recibido por Tecnología", Activo = true, EsFinal = true };
        var conIncidencia = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTecnologiaConIncidencia, Nombre = "Recibido por Tecnología con incidencia", Activo = true, EsFinal = true };
        var tipoEquipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        var tipoTransporte = new TipoTransporte
        {
            Codigo = estrategia == EstrategiaTransporteEnum.EntregaDirectaTecnologia ? "PRIVADO" : "INTERNO",
            Nombre = estrategia == EstrategiaTransporteEnum.EntregaDirectaTecnologia ? "Privado" : "Interno",
            Estrategia = estrategia,
            Activo = true
        };
        db.AddRange(filial, tecnologia, transito, recibidoTransportacion, recibido, conIncidencia, tipoEquipo, tipoTransporte);
        await db.SaveChangesAsync();

        // Las transiciones tal como quedan tras 20260910_RecepcionDirectaTransportePrivado.sql.
        db.TransicionesEstadoEnvio.AddRange(
            new TransicionEstadoEnvio { EstadoOrigenId = transito.EstadoEnvioId, EstadoDestinoId = recibido.EstadoEnvioId, Activo = true },
            new TransicionEstadoEnvio { EstadoOrigenId = transito.EstadoEnvioId, EstadoDestinoId = conIncidencia.EstadoEnvioId, Activo = true },
            new TransicionEstadoEnvio { EstadoOrigenId = recibidoTransportacion.EstadoEnvioId, EstadoDestinoId = recibido.EstadoEnvioId, Activo = true },
            new TransicionEstadoEnvio { EstadoOrigenId = recibidoTransportacion.EstadoEnvioId, EstadoDestinoId = conIncidencia.EstadoEnvioId, Activo = true });

        var equipo = new Equipo { TipoEquipoId = tipoEquipo.TipoEquipoId, UbicacionActualId = filial.UbicacionId, Marca = "HP", Modelo = "ProBook", NumeroSerie = "SN-PRIV-1" };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();

        var estadoInicial = await db.EstadosEnvio.FirstAsync(x => x.Codigo == codigoEstadoInicial);
        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-PRIVADO",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estadoInicial.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        db.Transportes.Add(new Transporte { EnvioId = envio.EnvioId, TipoTransporteId = tipoTransporte.TipoTransporteId });
        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "901",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Apertura del caso"
        });
        await db.SaveChangesAsync();
        return db;
    }
}

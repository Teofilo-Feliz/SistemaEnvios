using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Infrastructure.Services.Transportes;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Flujo interno (transportación institucional), sin los dos pasos de confirmación:
///   EN_FILIAL -> ENTREGADO_TRANSPORTACION -> EN_TRANSITO -> RECIBIDO_TRANSPORTACION -> RECIBIDO_TECNOLOGIA
/// El flujo privado no cambia y conserva ESPERA_TECNOLOGIA y EN_REVISION.
/// </summary>
public sealed class FlujoInternoTests
{
    private static readonly Guid UsuarioId = Guid.Parse("4a9c1b70-3e52-4d18-9f6a-8b2c7d0e1f34");

    [Fact]
    public async Task EntregarATransportacion_DejaElEnvioEntregadoSinPasoDeConfirmacion()
    {
        await using var db = CrearContexto();
        var (envio, estados) = await SembrarInternoAsync(db, EstadoEnvioCodigos.EnFilial);

        var resultado = await Estados(db).EntregarATransportacionAsync(envio.EnvioId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(estados[EstadoEnvioCodigos.EntregadoATransportacion].EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task ConfirmarTransporte_LlevaDeEntregadoATransito()
    {
        await using var db = CrearContexto();
        var (envio, estados) = await SembrarInternoAsync(db, EstadoEnvioCodigos.EntregadoATransportacion);
        var transporte = await db.Transportes.Include(x => x.Interno).FirstAsync();
        transporte.Interno!.FechaEntregaTransportacion = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var resultado = await Transportes(db).ConfirmarAsync(transporte.TransporteId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(estados[EstadoEnvioCodigos.EnTransito].EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task LlegadaTecnologia_Interno_TerminaEnRecibidoPorTransportacion()
    {
        await using var db = CrearContexto();
        var (envio, estados) = await SembrarInternoAsync(db, EstadoEnvioCodigos.EnTransito);

        var resultado = await Estados(db).RegistrarLlegadaTecnologiaAsync(envio.EnvioId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(estados[EstadoEnvioCodigos.RecibidoPorTransportacion].EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task LlegadaTecnologia_Privado_SigueLlegandoAEsperaDeTecnologia()
    {
        await using var db = CrearContexto();
        var (envio, estados) = await SembrarAsync(db, EstadoEnvioCodigos.EnTransito, EstrategiaTransporteEnum.EntregaDirectaTecnologia);

        var resultado = await Estados(db).RegistrarLlegadaTecnologiaAsync(envio.EnvioId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(estados[EstadoEnvioCodigos.EnEsperaDeTecnologia].EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task CambiarEstado_NoPermiteSaltarDeEntregadoATransitoAMano()
    {
        await using var db = CrearContexto();
        var (envio, estados) = await SembrarInternoAsync(db, EstadoEnvioCodigos.EntregadoATransportacion);

        var resultado = await Estados(db).CambiarAsync(new CambiarEstadoEnvioRequest
        {
            EnvioId = envio.EnvioId,
            EstadoDestinoId = estados[EstadoEnvioCodigos.EnTransito].EstadoEnvioId,
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    private static EstadoEnvioService Estados(SistemaEnviosDbContext db)
    {
        var usuario = Usuario();
        return new EstadoEnvioService(db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario));
    }

    private static TransporteService Transportes(SistemaEnviosDbContext db)
    {
        var usuario = Usuario();
        return new TransporteService(
            db, new UnitOfWork(db),
            new CrearTransporteRequestValidator(), new ActualizarTransporteRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario));
    }

    private static IUserContext Usuario() => FakeUserContext.Global(UsuarioId);

    private static Task<(Envio Envio, Dictionary<string, EstadoEnvio> Estados)> SembrarInternoAsync(
        SistemaEnviosDbContext db, string codigoActual) =>
        SembrarAsync(db, codigoActual, EstrategiaTransporteEnum.TransportacionInstitucional);

    private static async Task<(Envio, Dictionary<string, EstadoEnvio>)> SembrarAsync(
        SistemaEnviosDbContext db, string codigoActual, EstrategiaTransporteEnum estrategia)
    {
        string[] codigos = [
            EstadoEnvioCodigos.EnFilial, EstadoEnvioCodigos.EntregadoATransportacion,
            EstadoEnvioCodigos.DespachadoTransportePrivado, EstadoEnvioCodigos.EnTransito,
            EstadoEnvioCodigos.RecibidoPorTransportacion, EstadoEnvioCodigos.EnEsperaDeTecnologia,
            EstadoEnvioCodigos.EnProcesoDeRevision, EstadoEnvioCodigos.RecibidoPorTecnologia];
        var estados = codigos.ToDictionary(x => x, x => new EstadoEnvio
        {
            Codigo = x,
            Nombre = x,
            Activo = true,
            EsFinal = x == EstadoEnvioCodigos.RecibidoPorTecnologia,
        });
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnologia", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoTransporte { Codigo = "T", Nombre = "Tipo", Estrategia = estrategia, Activo = true };
        var chofer = new ChoferInterno { NombreCompleto = "Chofer", NumeroEmpleado = "E-1", Activo = true };
        db.AddRange(estados.Values);
        db.AddRange(filial, tecnologia, tipo, chofer);
        await db.SaveChangesAsync();

        // Solo las transiciones del flujo nuevo: el interno ya no pasa por confirmación.
        (string De, string A)[] transiciones = [
            (EstadoEnvioCodigos.EnFilial, EstadoEnvioCodigos.EntregadoATransportacion),
            (EstadoEnvioCodigos.EntregadoATransportacion, EstadoEnvioCodigos.EnTransito),
            (EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.RecibidoPorTransportacion),
            (EstadoEnvioCodigos.RecibidoPorTransportacion, EstadoEnvioCodigos.RecibidoPorTecnologia),
            (EstadoEnvioCodigos.EnFilial, EstadoEnvioCodigos.DespachadoTransportePrivado),
            (EstadoEnvioCodigos.DespachadoTransportePrivado, EstadoEnvioCodigos.EnTransito),
            (EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.EnEsperaDeTecnologia),
            (EstadoEnvioCodigos.EnEsperaDeTecnologia, EstadoEnvioCodigos.EnProcesoDeRevision),
            (EstadoEnvioCodigos.EnProcesoDeRevision, EstadoEnvioCodigos.RecibidoPorTecnologia)];
        db.TransicionesEstadoEnvio.AddRange(transiciones.Select(x => new TransicionEstadoEnvio
        {
            EstadoOrigenId = estados[x.De].EstadoEnvioId,
            EstadoDestinoId = estados[x.A].EstadoEnvioId,
            Activo = true,
        }));

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-FLUJO01",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estados[codigoActual].EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        var transporte = new Transporte { EnvioId = envio.EnvioId, TipoTransporteId = tipo.TipoTransporteId };
        if (estrategia == EstrategiaTransporteEnum.TransportacionInstitucional)
            transporte.Interno = new TransporteInterno { ChoferInternoId = chofer.ChoferInternoId, NombreChoferAlMomento = "Chofer", NumeroEmpleadoAlMomento = "E-1" };
        else
            transporte.Privado = new TransportePrivado { NombreResponsable = "R", Parentesco = "P", DocumentoResponsable = "1", PlacaVehiculo = "A1" };
        db.Transportes.Add(transporte);
        db.EnvioEquipos.Add(new EnvioEquipo { EnvioId = envio.EnvioId, EquipoId = 1, NumeroTicket = "999", Observaciones = "x", UsuarioSolicitanteId = UsuarioId });
        await db.SaveChangesAsync();
        return (envio, estados);
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}

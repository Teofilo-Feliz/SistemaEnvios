using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

public sealed class EstadoEnvioServiceTests
{
    private static readonly Guid UsuarioId = Guid.Parse("1dd33f3e-f927-48cb-b729-81f04017f65f");

    [Fact]
    public async Task CambiarAConfirmadoPorTransportacion_RequiereServicioEspecializado()
    {
        await using var db = CrearContexto();
        var origen = CrearEstado(EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion);
        var destino = CrearEstado(EstadoEnvioCodigos.ConfirmadoPorTransportacion);
        var ubicaciones = CrearUbicaciones();
        db.AddRange(origen, destino, ubicaciones.Origen, ubicaciones.Destino);
        await db.SaveChangesAsync();
        var envio = CrearEnvio(origen.EstadoEnvioId, ubicaciones.Origen.UbicacionId, ubicaciones.Destino.UbicacionId);
        db.Envios.Add(envio);
        db.TransicionesEstadoEnvio.Add(new TransicionEstadoEnvio
        {
            EstadoOrigenId = origen.EstadoEnvioId,
            EstadoDestinoId = destino.EstadoEnvioId,
            Activo = true
        });
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).CambiarAsync(new CambiarEstadoEnvioRequest
        {
            EnvioId = envio.EnvioId,
            EstadoDestinoId = destino.EstadoEnvioId
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        Assert.Equal(origen.EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task DespacharEnvio_RegistraFechaRealDeEntregaATransportacion()
    {
        await using var db = CrearContexto();
        var origen = CrearEstado(EstadoEnvioCodigos.EnFilial);
        var destino = CrearEstado(EstadoEnvioCodigos.EntregadoATransportacion);
        var ubicaciones = CrearUbicaciones();
        db.AddRange(origen, destino, ubicaciones.Origen, ubicaciones.Destino);
        await db.SaveChangesAsync();
        var envio = CrearEnvio(origen.EstadoEnvioId, ubicaciones.Origen.UbicacionId, ubicaciones.Destino.UbicacionId);
        db.Envios.Add(envio);
        await db.SaveChangesAsync();
        db.TransicionesEstadoEnvio.Add(new TransicionEstadoEnvio
        {
            EstadoOrigenId = origen.EstadoEnvioId,
            EstadoDestinoId = destino.EstadoEnvioId,
            Activo = true
        });
        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = 99,
            NumeroTicket = "T-99",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Prueba"
        });
        var tipo = new TipoTransporte { Codigo = "INTERNO", Nombre = "Interno", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        var chofer = new ChoferInterno { NombreCompleto = "Chofer prueba", NumeroEmpleado = "EMP-1", Activo = true };
        db.AddRange(tipo, chofer);
        await db.SaveChangesAsync();
        var interno = new TransporteInterno { ChoferInternoId = chofer.ChoferInternoId, NombreChoferAlMomento = chofer.NombreCompleto, NumeroEmpleadoAlMomento = chofer.NumeroEmpleado };
        var transporte = new Transporte { EnvioId = envio.EnvioId, TipoTransporteId = tipo.TipoTransporteId, Interno = interno };
        db.Transportes.Add(transporte);
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).CambiarAsync(new CambiarEstadoEnvioRequest
        {
            EnvioId = envio.EnvioId,
            EstadoDestinoId = destino.EstadoEnvioId
        });

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(interno.FechaEntregaTransportacion);
        Assert.Equal(destino.EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task TransportePrivado_OmiteConfirmacionYEntraDirectamenteATecnologia()
    {
        await using var db = CrearContexto();
        var filial = CrearEstado(EstadoEnvioCodigos.EnFilial);
        var privado = CrearEstado(EstadoEnvioCodigos.DespachadoTransportePrivado);
        var transito = CrearEstado(EstadoEnvioCodigos.EnTransito);
        var espera = CrearEstado(EstadoEnvioCodigos.EnEsperaDeTecnologia);
        var ubicaciones = CrearUbicaciones();
        db.AddRange(filial, privado, transito, espera, ubicaciones.Origen, ubicaciones.Destino);
        await db.SaveChangesAsync();
        var envio = CrearEnvio(filial.EstadoEnvioId, ubicaciones.Origen.UbicacionId, ubicaciones.Destino.UbicacionId);
        db.Envios.Add(envio);
        db.EnvioEquipos.Add(new EnvioEquipo { Envio = envio, EquipoId = 101, NumeroTicket = "T-101", UsuarioSolicitanteId = UsuarioId, Observaciones = "Prueba" });
        var tipo = new TipoTransporte { Codigo = "PRIVADO", Nombre = "Privado", Estrategia = EstrategiaTransporteEnum.EntregaDirectaTecnologia, Activo = true };
        db.Add(tipo); await db.SaveChangesAsync();
        db.Transportes.Add(new Transporte { EnvioId = envio.EnvioId, TipoTransporteId = tipo.TipoTransporteId, Privado = new TransportePrivado { NombreResponsable = "Juan Pérez", Parentesco = "Padre", CedulaResponsable = "00112345678", PlacaVehiculo = "A123456" } });
        db.TransicionesEstadoEnvio.AddRange(
            new TransicionEstadoEnvio { EstadoOrigenId = filial.EstadoEnvioId, EstadoDestinoId = privado.EstadoEnvioId, Activo = true },
            new TransicionEstadoEnvio { EstadoOrigenId = privado.EstadoEnvioId, EstadoDestinoId = transito.EstadoEnvioId, Activo = true },
            new TransicionEstadoEnvio { EstadoOrigenId = transito.EstadoEnvioId, EstadoDestinoId = espera.EstadoEnvioId, Activo = true });
        await db.SaveChangesAsync();
        var servicio = CrearServicio(db);

        var entrega = await servicio.EntregarTransportePrivadoAsync(envio.EnvioId);
        var llegada = await servicio.RegistrarLlegadaTecnologiaAsync(envio.EnvioId);

        Assert.True(entrega.IsSuccess); Assert.True(llegada.IsSuccess);
        Assert.Equal(espera.EstadoEnvioId, envio.EstadoEnvioId);
        Assert.Equal(3, await db.HistorialEstadosEnvio.CountAsync(x => x.EnvioId == envio.EnvioId));
    }

    private static EstadoEnvio CrearEstado(string codigo) => new()
    {
        Codigo = codigo,
        Nombre = codigo,
        Activo = true
    };

    private static (Ubicacion Origen, Ubicacion Destino) CrearUbicaciones() =>
        (new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true },
         new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true });

    private static Envio CrearEnvio(int estadoId, int origenId, int destinoId) => new()
    {
        NumeroEnvio = Guid.NewGuid().ToString("N"),
        UbicacionOrigenId = origenId,
        UbicacionDestinoId = destinoId,
        EstadoEnvioId = estadoId,
        Direccion = DireccionEnvioEnum.HaciaTecnologia,
        UsuarioSolicitanteId = UsuarioId
    };

    private static EstadoEnvioService CrearServicio(SistemaEnviosDbContext db) =>
        new(db, new UnitOfWork(db), new FakeUserContext(UsuarioId));

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

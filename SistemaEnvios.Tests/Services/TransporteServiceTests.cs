using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Transportes;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

public sealed class TransporteServiceTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task ConfirmarEntrega_DejaEnvioEnTransitoEnUnSoloPaso()
    {
        await using var db = CrearContexto();
        var entregado = CrearEstado(EstadoEnvioCodigos.EntregadoATransportacion);
        
        var enTransito = CrearEstado(EstadoEnvioCodigos.EnTransito);
        var origen = new Ubicacion
        {
            Nombre = "Filial",
            CodigoCentro = "FIL",
            Tipo = TipoUbicacionEnum.Filial,
            Activo = true
        };
        var destino = new Ubicacion
        {
            Nombre = "Tecnología",
            CodigoCentro = "TEC",
            Tipo = TipoUbicacionEnum.Tecnologia,
            Activo = true
        };
        var tipo = new TipoTransporte { Codigo = "INTERNO", Nombre = "Interno", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        var chofer = new ChoferInterno { NombreCompleto = "Chofer prueba", NumeroEmpleado = "EMP-1", Activo = true };
        db.AddRange(entregado, enTransito, origen, destino, tipo, chofer);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-PRUEBA-TRANSITO",
            UbicacionOrigenId = origen.UbicacionId,
            UbicacionDestinoId = destino.UbicacionId,
            EstadoEnvioId = entregado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        db.TransicionesEstadoEnvio.Add(
            new TransicionEstadoEnvio
            {
                EstadoOrigenId = entregado.EstadoEnvioId,
                EstadoDestinoId = enTransito.EstadoEnvioId,
                Activo = true
            });
        var transporte = new Transporte
        {
            Envio = envio,
            TipoTransporteId = tipo.TipoTransporteId,
            Interno = new TransporteInterno { ChoferInternoId = chofer.ChoferInternoId, NombreChoferAlMomento = chofer.NombreCompleto, NumeroEmpleadoAlMomento = chofer.NumeroEmpleado, FechaEntregaTransportacion = DateTime.UtcNow.AddMinutes(-5) }
        };
        db.Transportes.Add(transporte);
        await db.SaveChangesAsync();

        var servicio = new TransporteService(
            db,
            new UnitOfWork(db),
            new CrearTransporteRequestValidator(),
            new ActualizarTransporteRequestValidator(),
            FakeUserContext.Global(UsuarioId), AlcanceDePrueba.Crear(db, FakeUserContext.Global(UsuarioId)));

        var resultado = await servicio.ConfirmarAsync(transporte.TransporteId);

        Assert.True(resultado.IsSuccess);
        Assert.True(transporte.Interno!.EntregaConfirmada);
        Assert.NotNull(transporte.Interno.FechaConfirmacionEntrega);
        Assert.Equal(enTransito.EstadoEnvioId, envio.EstadoEnvioId);
        var historial = await db.HistorialEstadosEnvio
            .Where(x => x.EnvioId == envio.EnvioId)
            .OrderBy(x => x.HistorialEstadoEnvioId)
            .Select(x => x.EstadoEnvioId)
            .ToListAsync();
        Assert.Equal([enTransito.EstadoEnvioId], historial);
    }

    private static EstadoEnvio CrearEstado(string codigo) => new()
    {
        Codigo = codigo,
        Nombre = codigo,
        Activo = true
    };

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}

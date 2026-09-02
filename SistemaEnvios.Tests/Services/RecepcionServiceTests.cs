using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

public sealed class RecepcionServiceTests
{
    [Fact]
    public async Task CompletarRecepcion_MueveEquiposAlDestinoYLiberaReservas()
    {
        await using var db = CrearContexto();
        var usuarioId = Guid.NewGuid();
        var origen = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var destino = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var estadoActual = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoEnFilial, Nombre = "Recibido", Activo = true };
        var estadoFinal = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecepcionValidadaEnFilial, Nombre = "Validado", Activo = true, EsFinal = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(origen, destino, estadoActual, estadoFinal, tipo);
        await db.SaveChangesAsync();
        var equipo = new Equipo
        {
            Marca = "Marca",
            Modelo = "Modelo",
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = origen.UbicacionId
        };
        var envio = new Envio
        {
            NumeroEnvio = "ENV-RECEPCION",
            UbicacionOrigenId = origen.UbicacionId,
            UbicacionDestinoId = destino.UbicacionId,
            EstadoEnvioId = estadoActual.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaFilial,
            UsuarioSolicitanteId = usuarioId
        };
        db.AddRange(equipo, envio);
        await db.SaveChangesAsync();
        var detalle = new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "T-1",
            Observaciones = "Prueba",
            UsuarioSolicitanteId = usuarioId
        };
        var recepcion = new Recepcion
        {
            EnvioId = envio.EnvioId,
            EstadoRecepcion = EstadoRecepcionEnum.EnProceso,
            UsuarioQueAsignoId = usuarioId
        };
        db.AddRange(detalle, recepcion);
        await db.SaveChangesAsync();
        db.AddRange(
            new RecepcionEquipo
            {
                RecepcionId = recepcion.RecepcionId,
                EnvioEquipoId = detalle.EnvioEquipoId,
                EstadoRecepcionEquipo = EstadoRecepcionEquipoEnum.Verificado
            },
            new ReservaEquipoEnvio
            {
                EquipoId = equipo.EquipoId,
                EnvioId = envio.EnvioId,
                FechaReserva = DateTime.UtcNow,
                UsuarioId = usuarioId
            },
            new TransicionEstadoEnvio
            {
                EstadoOrigenId = estadoActual.EstadoEnvioId,
                EstadoDestinoId = estadoFinal.EstadoEnvioId,
                Activo = true
            });
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db, usuarioId).CompletarAsync(recepcion.RecepcionId);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(destino.UbicacionId, equipo.UbicacionActualId);
        Assert.Empty(await db.ReservasEquipoEnvio.ToListAsync());
        Assert.Equal(estadoFinal.EstadoEnvioId, envio.EstadoEnvioId);
    }

    private static RecepcionService CrearServicio(SistemaEnviosDbContext db, Guid usuarioId) => new(
        db,
        new UnitOfWork(db),
        new CrearRecepcionRequestValidator(),
        new VerificarEquipoRequestValidator(),
        new AsignarTecnicoRequestValidator(),
        FakeUserContext.Global(usuarioId), new AlcanceEnvios(db, FakeUserContext.Global(usuarioId)));

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

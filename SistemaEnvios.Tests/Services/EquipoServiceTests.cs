using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Validators.Equipos;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

public sealed class EquipoServiceTests
{
    [Fact]
    public async Task CambiarUbicacionDeEquipoReservado_RetornaConflicto()
    {
        await using var db = CrearContexto();
        var usuarioId = Guid.NewGuid();
        var origen = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var destino = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = Domain.Constants.EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(origen, destino, estado, tipo);
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
            NumeroEnvio = "ENV-RESERVA",
            UbicacionOrigenId = origen.UbicacionId,
            UbicacionDestinoId = destino.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = usuarioId
        };
        db.AddRange(equipo, envio);
        await db.SaveChangesAsync();
        db.ReservasEquipoEnvio.Add(new ReservaEquipoEnvio
        {
            EquipoId = equipo.EquipoId,
            EnvioId = envio.EnvioId,
            FechaReserva = DateTime.UtcNow,
            UsuarioId = usuarioId
        });
        await db.SaveChangesAsync();
        var service = new EquipoService(
            db,
            new UnitOfWork(db),
            new CrearEquipoRequestValidator(),
            new ActualizarEquipoRequestValidator(),
            FakeUserContext.Global(usuarioId),
            new AlcanceEnvios(db, FakeUserContext.Global(usuarioId)));

        var resultado = await service.ActualizarAsync(new ActualizarEquipoRequest
        {
            EquipoId = equipo.EquipoId,
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = destino.UbicacionId,
            Marca = equipo.Marca,
            Modelo = equipo.Modelo
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        Assert.Equal(origen.UbicacionId, equipo.UbicacionActualId);
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories.Envios;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

public sealed class EnvioEquipoServiceTests
{
    [Fact]
    public async Task AgregarEquipoQuePerteneceAOtroEnvioActivo_RetornaConflicto()
    {
        await using var db = CrearContexto();
        var usuarioId = Guid.NewGuid();
        var estadoActivo = new EstadoEnvio { Codigo = Domain.Constants.EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var ubicacion = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = Domain.Enums.TipoUbicacionEnum.Filial, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(estadoActivo, ubicacion, tipo);
        await db.SaveChangesAsync();
        var equipo = new Equipo { Marca = "Marca", Modelo = "Modelo", TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = ubicacion.UbicacionId };
        var primerEnvio = CrearEnvio(estadoActivo.EstadoEnvioId, ubicacion.UbicacionId, usuarioId);
        var segundoEnvio = CrearEnvio(estadoActivo.EstadoEnvioId, ubicacion.UbicacionId, usuarioId);
        db.AddRange(equipo, primerEnvio, segundoEnvio);
        await db.SaveChangesAsync();
        db.EnvioEquipos.Add(new EnvioEquipo { EnvioId = primerEnvio.EnvioId, EquipoId = equipo.EquipoId, NumeroTicket = "T-1", UsuarioSolicitanteId = usuarioId, Observaciones = "Falla" });
        db.ReservasEquipoEnvio.Add(new ReservaEquipoEnvio
        {
            EquipoId = equipo.EquipoId,
            EnvioId = primerEnvio.EnvioId,
            FechaReserva = DateTime.UtcNow,
            UsuarioId = usuarioId
        });
        await db.SaveChangesAsync();
        var service = CrearServicio(db, usuarioId);

        var resultado = await service.AgregarAsync(new AgregarEquipoEnvioRequest
        {
            EnvioId = segundoEnvio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "T-2",
            Observaciones = "Otra falla"
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    [Fact]
    public async Task AgregarEquipoFueraDeLaUbicacionOrigen_RetornaConflicto()
    {
        await using var db = CrearContexto();
        var usuarioId = Guid.NewGuid();
        var estado = new EstadoEnvio { Codigo = Domain.Constants.EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var origen = new Ubicacion { Nombre = "Filial A", CodigoCentro = "A", Tipo = Domain.Enums.TipoUbicacionEnum.Filial, Activo = true };
        var otraFilial = new Ubicacion { Nombre = "Filial B", CodigoCentro = "B", Tipo = Domain.Enums.TipoUbicacionEnum.Filial, Activo = true };
        var destino = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = Domain.Enums.TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(estado, origen, otraFilial, destino, tipo);
        await db.SaveChangesAsync();
        var equipo = new Equipo
        {
            Marca = "Marca",
            Modelo = "Modelo",
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = otraFilial.UbicacionId
        };
        var envio = CrearEnvio(estado.EstadoEnvioId, origen.UbicacionId, usuarioId);
        envio.UbicacionDestinoId = destino.UbicacionId;
        db.AddRange(equipo, envio);
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db, usuarioId).AgregarAsync(new AgregarEquipoEnvioRequest
        {
            EnvioId = envio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "T-3",
            Observaciones = "Prueba"
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        Assert.Empty(db.ReservasEquipoEnvio);
    }

    private static EnvioEquipoService CrearServicio(SistemaEnviosDbContext db, Guid usuarioId) => new(
        new EnvioEquipoRepository(db),
        new UnitOfWork(db),
        new AgregarEquipoEnvioRequestValidator(),
        new ActualizarEnvioEquipoRequestValidator(),
        db,
        new FakeUserContext(usuarioId));

    private static Envio CrearEnvio(int estadoId, int ubicacionId, Guid usuarioId) => new()
    {
        NumeroEnvio = Guid.NewGuid().ToString("N"),
        UbicacionOrigenId = ubicacionId,
        UbicacionDestinoId = ubicacionId + 1,
        EstadoEnvioId = estadoId,
        Direccion = Domain.Enums.DireccionEnvioEnum.HaciaTecnologia,
        UsuarioSolicitanteId = usuarioId
    };

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

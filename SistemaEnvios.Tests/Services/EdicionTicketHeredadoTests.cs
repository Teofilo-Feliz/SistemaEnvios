using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories.Envios;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El equipo que vuelve a la filial sale con el mismo ticket con que llegó: ese número es del
/// caso, no de quien edita. Por eso una continuación no se consulta contra GLPI —si el ticket se
/// depuró allá, corregirle las observaciones al equipo de vuelta quedaría bloqueado para siempre.
/// La apertura sí se consulta: ahí el número lo escribe una persona.
/// </summary>
public sealed class EdicionTicketHeredadoTests
{
    [Fact]
    public async Task LaContinuacionSeEditaAunqueGlpiNoTengaElTicketHeredado()
    {
        await using var db = CrearContexto();
        var (apertura, continuacion) = await SembrarAsync(db);

        // Un validador que rechaza todo: si la continuación lo consultara, esto fallaría.
        var resultado = await Servicio(db, ValidadorTicketDePrueba.QueRechaza()).ActualizarAsync(new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = continuacion.EnvioEquipoId,
            NumeroTicket = continuacion.NumeroTicket,
            Observaciones = "Se corrige la nota del equipo devuelto.",
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        var guardada = (await db.EnvioEquipos.FindAsync(continuacion.EnvioEquipoId))!;
        Assert.Equal("Se corrige la nota del equipo devuelto.", guardada.Observaciones);
        // Y el ticket sigue siendo el de la apertura, no uno nuevo.
        Assert.Equal(apertura.NumeroTicket, guardada.NumeroTicket);
    }

    [Fact]
    public async Task LaContinuacionNoCambiaDeTicketAunqueSeEnvieOtro()
    {
        await using var db = CrearContexto();
        var (apertura, continuacion) = await SembrarAsync(db);

        var resultado = await Servicio(db, ValidadorTicketDePrueba.QueAcepta()).ActualizarAsync(new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = continuacion.EnvioEquipoId,
            NumeroTicket = "888888",
            Observaciones = "Intento de cambiarle el ticket a la devolución.",
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(apertura.NumeroTicket,
            (await db.EnvioEquipos.FindAsync(continuacion.EnvioEquipoId))!.NumeroTicket);
    }

    [Fact]
    public async Task LaAperturaSiSeRechazaCuandoGlpiNoTieneElTicket()
    {
        await using var db = CrearContexto();
        var (apertura, _) = await SembrarAsync(db);

        var resultado = await Servicio(db, ValidadorTicketDePrueba.QueRechaza()).ActualizarAsync(new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = apertura.EnvioEquipoId,
            NumeroTicket = "777777",
            Observaciones = "Se corrige el ticket a mano.",
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
    }

    private static async Task<(EnvioEquipo Apertura, EnvioEquipo Continuacion)> SembrarAsync(SistemaEnviosDbContext db)
    {
        var usuarioId = FakeUserContext.Global(Guid.NewGuid()).UserId!.Value;
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var ubicacion = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(estado, ubicacion, tipo);
        await db.SaveChangesAsync();

        var equipo = new Equipo { Marca = "Dell", Modelo = "Latitude", TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = ubicacion.UbicacionId };
        var envioIda = CrearEnvio(estado.EstadoEnvioId, ubicacion.UbicacionId, usuarioId);
        var envioVuelta = CrearEnvio(estado.EstadoEnvioId, ubicacion.UbicacionId, usuarioId);
        db.AddRange(equipo, envioIda, envioVuelta);
        await db.SaveChangesAsync();

        var apertura = new EnvioEquipo
        {
            EnvioId = envioIda.EnvioId, EquipoId = equipo.EquipoId, NumeroTicket = "25000",
            UsuarioSolicitanteId = usuarioId, Observaciones = "Ida"
        };
        db.EnvioEquipos.Add(apertura);
        await db.SaveChangesAsync();

        // La vuelta repite el ticket de la ida y queda encadenada a ella.
        var continuacion = new EnvioEquipo
        {
            EnvioId = envioVuelta.EnvioId, EquipoId = equipo.EquipoId, NumeroTicket = apertura.NumeroTicket,
            EnvioEquipoOrigenId = apertura.EnvioEquipoId, UsuarioSolicitanteId = usuarioId, Observaciones = "Vuelta"
        };
        db.EnvioEquipos.Add(continuacion);
        await db.SaveChangesAsync();
        return (apertura, continuacion);
    }

    private static EnvioEquipoService Servicio(SistemaEnviosDbContext db, IValidadorTicketGlpi validador)
    {
        var usuario = FakeUserContext.Global(Guid.NewGuid());
        return new EnvioEquipoService(
            new EnvioEquipoRepository(db), new UnitOfWork(db),
            new AgregarEquipoEnvioRequestValidator(), new ActualizarEnvioEquipoRequestValidator(),
            db, usuario, AlcanceDePrueba.Crear(db, usuario),
            new CasoEquipoService(db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario)),
            validador);
    }

    private static Envio CrearEnvio(int estadoId, int ubicacionId, Guid usuarioId) => new()
    {
        NumeroEnvio = Guid.NewGuid().ToString("N"),
        UbicacionOrigenId = ubicacionId,
        UbicacionDestinoId = ubicacionId + 1,
        EstadoEnvioId = estadoId,
        Direccion = DireccionEnvioEnum.HaciaTecnologia,
        UsuarioSolicitanteId = usuarioId
    };

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}

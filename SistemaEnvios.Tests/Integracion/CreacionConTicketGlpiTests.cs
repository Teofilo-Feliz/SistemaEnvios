using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Integrations.Glpi;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// El flujo completo tal como lo vive el usuario: escribe un número en el campo Ticket y crea el
/// envío. Va contra GLPI real y contra el servicio real de creación, porque lo que se comprueba
/// es precisamente que el número escrito se consulte de verdad y que un ticket inventado no
/// llegue a la base.
/// </summary>
public sealed class CreacionConTicketGlpiTests
{
    private static readonly Guid UsuarioId = Guid.Parse("5d8f2a71-4b0c-4e93-a6d5-91c3f7e08b24");
    private const string TicketReal = "25000";
    private const string TicketInventado = "99999999";

    [SkippableFact]
    public async Task UnTicketQueNoExisteEnGlpiNoLlegaALaBase()
    {
        var validador = ValidadorRealOSalte();
        await using var db = await SembrarAsync();
        var (filial, tecnologia) = await UbicacionesAsync(db);
        var equipo = await db.Equipos.FirstAsync();

        var resultado = await Servicio(db, validador).CrearConEquiposAsync(new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            Equipos = [new() { EquipoId = equipo.EquipoId, NumeroTicket = TicketInventado }],
        });

        Assert.True(resultado.IsFailure);
        Assert.Contains("no existe en la mesa de ayuda", resultado.Error);
        // Lo que importa no es solo el mensaje: el envío no puede haberse creado a medias.
        Assert.Empty(await db.Envios.ToListAsync());
        Assert.Empty(await db.EnvioEquipos.ToListAsync());
    }

    [SkippableFact]
    public async Task UnTicketRealDeGlpiCreaElEnvio()
    {
        var validador = ValidadorRealOSalte();
        await using var db = await SembrarAsync();
        var (filial, tecnologia) = await UbicacionesAsync(db);
        var equipo = await db.Equipos.FirstAsync();

        var resultado = await Servicio(db, validador).CrearConEquiposAsync(new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            Equipos = [new() { EquipoId = equipo.EquipoId, NumeroTicket = TicketReal }],
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Single(await db.Envios.ToListAsync());
        var detalle = Assert.Single(await db.EnvioEquipos.ToListAsync());
        Assert.Equal(TicketReal, detalle.NumeroTicket);
    }

    private static IValidadorTicketGlpi ValidadorRealOSalte()
    {
        var valores = new Dictionary<string, string?>
        {
            ["Glpi:BaseUrl"] = Environment.GetEnvironmentVariable("GLPI_BASE_URL"),
            ["Glpi:AppToken"] = Environment.GetEnvironmentVariable("GLPI_APP_TOKEN"),
            ["Glpi:Username"] = Environment.GetEnvironmentVariable("GLPI_USERNAME"),
            ["Glpi:Password"] = Environment.GetEnvironmentVariable("GLPI_PASSWORD")
        };

        Skip.If(
            valores.Values.Any(string.IsNullOrWhiteSpace),
            "Defina GLPI_BASE_URL, GLPI_APP_TOKEN, GLPI_USERNAME y GLPI_PASSWORD para ejecutar contra GLPI.");

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddGlpi(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());
        var glpi = servicios.BuildServiceProvider().GetRequiredService<IGlpiClient>();
        return new ValidadorTicketGlpi(glpi, NullLogger<ValidadorTicketGlpi>.Instance);
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db, IValidadorTicketGlpi validador)
    {
        var usuario = FakeUserContext.Global(UsuarioId);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario),
            new CasoEquipoService(db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario)),
            validador);
    }

    private static async Task<(Ubicacion Filial, Ubicacion Tecnologia)> UbicacionesAsync(SistemaEnviosDbContext db) =>
        (await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Filial),
         await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Tecnologia));

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
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
            Modelo = "Latitude",
            NumeroSerie = "SN-1",
            CodigoActivo = "ADR-1",
        });
        await db.SaveChangesAsync();
        return db;
    }
}

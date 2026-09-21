using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Integrations.Glpi;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Infrastructure;

public sealed class EquipoDeTicketGlpiTests
{
    [Fact]
    public async Task TraeMarcaModeloYSerialDelEquipoDelTicket()
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, Ticket(("Computer", 657)), Hp());

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsSuccess, resultado.Error);
        var equipo = resultado.Value!.Equipo;
        Assert.NotNull(equipo);
        Assert.Equal("Hewlett-Packard", equipo.Marca);
        Assert.Equal("HP Compaq 8000 Elite SFF PC", equipo.Modelo);
        Assert.Equal("MXL04916TL", equipo.Serial);
    }

    /// <summary>
    /// La política, aplicada también aquí y con el mismo texto que al guardar: si dijera otra cosa
    /// el usuario creería que son dos problemas distintos.
    /// </summary>
    [Fact]
    public async Task RechazaUnTicketConVariosEquipos()
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, Ticket(("Computer", 1), ("Monitor", 2)), Hp());

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains("2 equipos asociados", resultado.Error);
    }

    /// <summary>Sin equipos no hay nada que autocompletar, pero tampoco es un error.</summary>
    [Fact]
    public async Task UnTicketSinEquiposDevuelveEquipoEnNulo()
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, Ticket(), Hp());

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Null(resultado.Value!.Equipo);
    }

    /// <summary>
    /// Se consulta nuestra base antes que GLPI: un ticket que ya abrió un caso no se puede volver
    /// a usar, y preguntar a la mesa de ayuda serían dos llamadas para nada.
    /// </summary>
    [Fact]
    public async Task UnTicketYaUsadoNoLlegaAConsultarGlpi()
    {
        await using var db = await BaseAsync();
        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = 1,
            EquipoId = 1,
            NumeroTicket = "30261",
            EnvioEquipoOrigenId = null,
            Observaciones = "x",
        });
        await db.SaveChangesAsync();

        var servicio = new EquipoDeTicketGlpi(new GlpiQueExplota(), db, NullLogger<EquipoDeTicketGlpi>.Instance);
        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    [Fact]
    public async Task UnTicketQueGlpiNoTieneSeRechaza()
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, TicketGlpi.NoEncontrado, Hp());

        var resultado = await servicio.ObtenerAsync("99999999");

        Assert.True(resultado.IsFailure);
        Assert.Contains("no existe en la mesa de ayuda", resultado.Error);
    }

    [Theory]
    [InlineData("Computer", "Notebook", "Laptop")]
    [InlineData("Computer", "Low Profile Desktop", "Computadora de escritorio")]
    [InlineData("Computer", null, "Computadora de escritorio")]
    [InlineData("Monitor", null, "Monitor")]
    [InlineData("Printer", null, "Impresora")]
    public async Task TraduceElTipoDeGlpiAlNuestro(string itemType, string? tipoGlpi, string esperado)
    {
        await using var db = await BaseAsync();
        var equipo = Hp() with { ItemType = itemType, TipoGlpi = tipoGlpi };
        var servicio = Servicio(db, Ticket((itemType, 1)), equipo);

        var resultado = await servicio.ObtenerAsync("30261");

        var tipoId = resultado.Value!.Equipo!.TipoEquipoId;
        Assert.Equal(esperado, db.TiposEquipo.First(x => x.TipoEquipoId == tipoId).Nombre);
    }

    /// <summary>
    /// Un tipo que no sabemos leer no gasta la segunda llamada: se devuelve sin equipo y la
    /// persona escribe los datos.
    /// </summary>
    [Fact]
    public async Task UnTipoNoSoportadoNoConsultaElActivo()
    {
        await using var db = await BaseAsync();
        var servicio = new EquipoDeTicketGlpi(
            new GlpiFalso(Result<TicketGlpi>.Success(Ticket(("Software", 9))), null),
            db, NullLogger<EquipoDeTicketGlpi>.Instance);

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Null(resultado.Value!.Equipo);
    }

    /// <summary>
    /// GLPI tiene papelera y plantillas, y las dos responden 200. Dar de alta una plantilla como
    /// equipo metería basura en el inventario.
    /// </summary>
    [Fact]
    public async Task NoAutocompletaDesdeLaPapeleraNiUnaPlantilla()
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, Ticket(("Computer", 657)), Hp() with { EnPapeleraOPlantilla = true });

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Null(resultado.Value!.Equipo);
    }

    /// <summary>El código de activo de ADRTrack es numérico; el otherserial de GLPI es texto libre.</summary>
    [Fact]
    public async Task DescartaUnCodigoDeActivoConLetras()
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, Ticket(("Computer", 657)), Hp() with { CodigoActivo = "ADR-118" });

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.Null(resultado.Value!.Equipo!.CodigoActivo);
    }

    [Fact]
    public async Task AvisaCuandoElEquipoYaViajaEnOtroEnvio()
    {
        await using var db = await BaseAsync();
        db.Equipos.Add(new Equipo
        {
            EquipoId = 50, NumeroSerie = "MXL04916TL", Marca = "HP", Modelo = "x",
            TipoEquipoId = 1, UbicacionActualId = 1,
        });
        db.ReservasEquipoEnvio.Add(new ReservaEquipoEnvio { EquipoId = 50, EnvioId = 7 });
        await db.SaveChangesAsync();

        var servicio = Servicio(db, Ticket(("Computer", 657)), Hp());
        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.Value!.Equipo!.YaEstaEnOtroEnvio);
    }

    /// <summary>
    /// El estado se comprueba también al buscar, y con el mismo texto que al guardar: así la
    /// persona se entera antes de llenar el formulario y no después.
    /// </summary>
    [Theory]
    [InlineData(1, "Nuevo")]
    [InlineData(5, "Resuelto")]
    [InlineData(6, "Cerrado")]
    public async Task NoAutocompletaConUnTicketQueNoEstaEnCurso(int estado, string nombre)
    {
        await using var db = await BaseAsync();
        var servicio = Servicio(db, Ticket(estado, ("Computer", 657)), Hp());

        var resultado = await servicio.ObtenerAsync("30261");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains(nombre, resultado.Error);
    }

    private static TicketGlpi Ticket(params (string Tipo, int Id)[] equipos) =>
        Ticket(EstadoTicketGlpi.EnCurso, equipos);

    private static TicketGlpi Ticket(int estado, params (string Tipo, int Id)[] equipos) =>
        new(true, [.. equipos.Select(x => new ItemDeTicketGlpi(x.Tipo, x.Id))], estado);

    private static EquipoGlpi Hp() => new(
        "Computer", "Hewlett-Packard", "HP Compaq 8000 Elite SFF PC", "MXL04916TL",
        null, "LAB-INFORMATICA", "Low Profile Desktop", false);

    private static EquipoDeTicketGlpi Servicio(SistemaEnviosDbContext db, TicketGlpi ticket, EquipoGlpi equipo) =>
        new(new GlpiFalso(Result<TicketGlpi>.Success(ticket), equipo), db,
            NullLogger<EquipoDeTicketGlpi>.Instance);

    private static async Task<SistemaEnviosDbContext> BaseAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TiposEquipo.AddRange(
            new TipoEquipo { TipoEquipoId = 1, Nombre = "Laptop", Activo = true },
            new TipoEquipo { TipoEquipoId = 2, Nombre = "Computadora de escritorio", Activo = true },
            new TipoEquipo { TipoEquipoId = 3, Nombre = "Monitor", Activo = true },
            new TipoEquipo { TipoEquipoId = 4, Nombre = "Impresora", Activo = true },
            new TipoEquipo { TipoEquipoId = 5, Nombre = "Otro", Activo = true });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class GlpiFalso(Result<TicketGlpi> ticket, EquipoGlpi? equipo) : IGlpiClient
    {
        public Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default) =>
            Task.FromResult(ticket);

        public Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default) =>
            equipo is null
                ? throw new InvalidOperationException("No debió consultarse el activo.")
                : Task.FromResult(Result<EquipoGlpi?>.Success(equipo));
    }

    private sealed class GlpiQueExplota : IGlpiClient
    {
        public Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default) =>
            throw new InvalidOperationException("No debió consultarse a GLPI.");

        public Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default) =>
            throw new InvalidOperationException("No debió consultarse a GLPI.");
    }
}

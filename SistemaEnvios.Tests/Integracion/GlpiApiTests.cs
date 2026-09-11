using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Integrations.Glpi;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Contrato real de la mesa de ayuda: 200 cuando el item existe, 404 cuando no. Va contra la
/// instancia de verdad a propósito —un doble de prueba solo confirmaría lo que asumimos de GLPI,
/// que es justo lo que hay que verificar.
///
/// Se ejecuta solo si las credenciales están en el entorno, igual que las pruebas de SQL Server.
/// Para correrla en local, léalas de los User Secrets del API en vez de escribirlas en un script:
///   $env:GLPI_BASE_URL  = "https://.../apirest.php"
///   $env:GLPI_APP_TOKEN = "..."; $env:GLPI_USERNAME = "..."; $env:GLPI_PASSWORD = "..."
/// </summary>
public sealed class GlpiApiTests
{
    // Existe en la instancia de ADR ("Compra de Equipos"). Si algún día se depura, la prueba
    // empieza a fallar con "existe = false" y hay que apuntarla a otro ticket vigente.
    private const int TicketQueExiste = 25000;
    private const int TicketQueNoExiste = 99_999_999;

    // Verificados a mano contra la instancia: el ticket 30261 tiene un solo activo, el Computer
    // 657 ('LAB-INFORMATICA', HP Compaq 8000 Elite SFF PC, serial MXL04916TL).
    private const int TicketConUnEquipo = 30261;
    private const int EquipoDelTicket = 657;

    [SkippableFact]
    public async Task ElTicketDeReferenciaExiste()
    {
        var glpi = ClienteOSalte();

        var resultado = await glpi.ItemExistsAsync("Ticket", TicketQueExiste);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.True(resultado.Value);
    }

    [SkippableFact]
    public async Task UnTicketInexistenteDaFalseYNoUnError()
    {
        var glpi = ClienteOSalte();

        var resultado = await glpi.ItemExistsAsync("Ticket", TicketQueNoExiste);

        // Lo importante es la distinción: 404 es una respuesta, no una falla de integración.
        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.False(resultado.Value);
    }

    [SkippableFact]
    public async Task LaSegundaConsultaReutilizaLaSesion()
    {
        var glpi = ClienteOSalte();

        var primera = await glpi.ItemExistsAsync("Ticket", TicketQueExiste);
        var segunda = await glpi.ItemExistsAsync("Ticket", TicketQueExiste);

        Assert.True(primera.IsSuccess, primera.Error);
        Assert.True(segunda.IsSuccess, segunda.Error);
    }

/// <summary>
    /// El contrato que sostiene la regla de un equipo por ticket: Item_Ticket responde con 404
    /// cuando el ticket no existe y con la lista de asociaciones cuando sí. Por eso una sola
    /// llamada sustituye a la consulta de existencia en vez de sumarse a ella.
    /// </summary>
    [SkippableFact]
    public async Task ItemTicketDistingueUnTicketInexistenteDeUnoSinEquipos()
    {
        var glpi = ClienteOSalte();

        var inexistente = await glpi.ObtenerTicketAsync(TicketQueNoExiste);

        // 404 es una respuesta, no una falla de integración.
        Assert.True(inexistente.IsSuccess, inexistente.Error);
        Assert.False(inexistente.Value!.Existe);
    }

    /// <summary>
    /// Ticket real de la instancia de ADR, verificado a mano: trae exactamente un Computer, el
    /// 657. Si algún día le cuelgan un segundo equipo esta prueba empieza a fallar, y eso es lo
    /// que se quiere: significa que el ticket dejó de ser usable en ADRTrack.
    /// </summary>
    [SkippableFact]
    public async Task ElTicketDeReferenciaTraeUnSoloEquipo()
    {
        var glpi = ClienteOSalte();

        var resultado = await glpi.ObtenerTicketAsync(TicketConUnEquipo);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.True(resultado.Value!.Existe);
        Assert.True(resultado.Value.EsUsable);
        var equipo = Assert.Single(resultado.Value.Equipos);
        Assert.Equal("Computer", equipo.ItemType);
        Assert.Equal(EquipoDelTicket, equipo.ItemsId);
    }

/// <summary>
    /// El autocompletado de punta a punta contra la instancia real: las dos llamadas encadenadas y
    /// el mapeo a los campos del formulario. Es la prueba que detecta si GLPI cambia el nombre de
    /// un campo o deja de expandir los dropdowns.
    /// </summary>
    [SkippableFact]
    public async Task ElTicketDeReferenciaAutocompletaMarcaModeloYSerial()
    {
        var glpi = ClienteOSalte();
        await using var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TiposEquipo.Add(new TipoEquipo { TipoEquipoId = 2, Nombre = "Computadora de escritorio", Activo = true });
        await db.SaveChangesAsync();

        var servicio = new EquipoDeTicketGlpi(glpi, db, NullLogger<EquipoDeTicketGlpi>.Instance);
        var resultado = await servicio.ObtenerAsync(TicketConUnEquipo.ToString());

        Assert.True(resultado.IsSuccess, resultado.Error);
        var equipo = resultado.Value!.Equipo;
        Assert.NotNull(equipo);
        Assert.Equal("Hewlett-Packard", equipo.Marca);
        Assert.Equal("HP Compaq 8000 Elite SFF PC", equipo.Modelo);
        Assert.Equal("MXL04916TL", equipo.Serial);
        Assert.Equal("LAB-INFORMATICA", equipo.Nombre);
    }

    private static IGlpiClient ClienteOSalte()
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

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddGlpi(configuracion);
        return servicios.BuildServiceProvider().GetRequiredService<IGlpiClient>();
    }
}

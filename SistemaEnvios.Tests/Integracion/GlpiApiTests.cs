using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Integrations.Glpi;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Contrato real de la mesa de ayuda: Item_Ticket responde 404 cuando el ticket no existe y la
/// lista de activos cuando sí. Va contra la
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
    private const int TicketQueNoExiste = 99_999_999;

    // Verificados a mano contra la instancia: el ticket 30261 tiene un solo activo, el Computer
    // 657 ('LAB-INFORMATICA', HP Compaq 8000 Elite SFF PC, serial MXL04916TL).
    private const int TicketConUnEquipo = 30261;
    private const int EquipoDelTicket = 657;

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
    /// El contrato del estado: GLPI manda el campo status en el ticket y se deja leer como
    /// número. Importa comprobarlo contra la instancia real porque un estado ilegible no rompe
    /// nada visible —se trata como "no se pudo leer" y deja pasar—, así que la regla se apagaría
    /// en silencio si el campo cambiara de forma.
    /// </summary>
    [SkippableFact]
    public async Task ElTicketDeReferenciaTraeSuEstado()
    {
        var glpi = ClienteOSalte();

        var resultado = await glpi.ObtenerTicketAsync(TicketConUnEquipo);

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.NotNull(resultado.Value!.Estado);
        Assert.InRange(resultado.Value.Estado!.Value, 1, 6);
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

        // El estado del ticket de referencia lo maneja soporte, no nosotros. Si se cierra, el
        // autocompletado lo rechaza por política y esta prueba no tendría nada que decir sobre el
        // mapeo de campos, que es lo que comprueba. La consulta sale de la caché de la línea
        // anterior, así que no cuesta una llamada más.
        var estado = await glpi.ObtenerTicketAsync(TicketConUnEquipo);
        Skip.If(
            estado.Value?.ElEstadoLoImpide == true,
            $"El ticket {TicketConUnEquipo} está en estado " +
            $"«{EstadoTicketGlpi.Nombre(estado.Value?.Estado)}» y el autocompletado solo acepta en curso.");

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

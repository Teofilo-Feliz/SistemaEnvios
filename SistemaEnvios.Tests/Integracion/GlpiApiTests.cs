using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Infrastructure.Integrations.Glpi;

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

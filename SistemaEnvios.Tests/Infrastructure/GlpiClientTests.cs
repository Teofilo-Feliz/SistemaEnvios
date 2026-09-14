using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Infrastructure.Integrations.Glpi;

namespace SistemaEnvios.Tests.Infrastructure;

/// <summary>
/// El itemType se concatena a la URL de GLPI, así que la validación es lo que impide que un
/// parámetro de la ruta reescriba el destino de la petición. Estas pruebas no tocan la red:
/// el cliente rechaza antes de llegar al HttpClient.
/// </summary>
public sealed class GlpiClientTests
{
    [Theory]
    [InlineData("../Ticket")]
    [InlineData("Ticket/25000")]
    [InlineData("Ticket?id=1")]
    [InlineData("")]
    [InlineData("1Ticket")]
    [InlineData("Ticket ")]
    public async Task RechazaUnItemTypeQueReescribiriaLaRuta(string itemType)
    {
        var resultado = await Cliente().ObtenerEquipoAsync(itemType, 657);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RechazaUnIdQueGlpiNuncaTendria(int id)
    {
        var resultado = await Cliente().ObtenerEquipoAsync("Computer", id);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
    }

    [Theory]
    [InlineData("Computer")]
    [InlineData("Monitor")]
    [InlineData("Item_DeviceProcessor")]
    public async Task AceptaLosItemTypesRealesDeGlpi(string itemType)
    {
        // Sin red disponible la consulta falla, pero lo que se comprueba es que NO fue rechazada
        // por validación: el itemType pasó el filtro y se intentó la llamada.
        var resultado = await Cliente(baseUrl: "http://localhost:1/apirest.php/")
            .ObtenerEquipoAsync(itemType, 1);

        Assert.NotEqual(ErrorType.Validation, resultado.ErrorType);
    }

    private static GlpiClient Cliente(string baseUrl = "http://localhost:1/apirest.php/")
    {
        var opciones = Options.Create(new GlpiOptions
        {
            BaseUrl = baseUrl,
            AppToken = "token-de-prueba",
            Username = "usuario",
            Password = "clave",
            TimeoutSeconds = 1
        });

        var servicios = new ServiceCollection();
        servicios.AddHttpClient(GlpiSessionProvider.HttpClientName, http =>
        {
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(1);
        });
        var proveedorHttp = servicios.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();

        var sesion = new GlpiSessionProvider(proveedorHttp, opciones, NullLogger<GlpiSessionProvider>.Instance);
        var http = proveedorHttp.CreateClient(GlpiSessionProvider.HttpClientName);
        // Caché nueva por cliente: si se compartiera, una prueba serviría la respuesta de otra.
        return new GlpiClient(
            http, sesion, new MemoryCache(new MemoryCacheOptions()), NullLogger<GlpiClient>.Instance);
    }
}

using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SistemaEnvios.Infrastructure.Integrations.Glpi;

namespace SistemaEnvios.Tests.Infrastructure;

/// <summary>
/// El mismo ticket se consulta dos veces seguidas: al autocompletar el formulario y otra vez al
/// guardar, cuando el servidor lo vuelve a comprobar por su cuenta. La segunda no debe salir a la
/// red.
/// </summary>
public sealed class GlpiCacheTests
{
    [Fact]
    public async Task LaSegundaConsultaDelMismoTicketNoVuelveAGlpi()
    {
        var contador = new ContadorDeLlamadas();
        var cliente = Cliente(contador);

        await cliente.ObtenerTicketAsync(30261);
        var trasLaPrimera = contador.Peticiones;
        await cliente.ObtenerTicketAsync(30261);

        Assert.True(trasLaPrimera > 0, "La primera consulta sí debe salir a la red.");
        Assert.Equal(trasLaPrimera, contador.Peticiones);
    }

    [Fact]
    public async Task UnTicketDistintoSiConsulta()
    {
        var contador = new ContadorDeLlamadas();
        var cliente = Cliente(contador);

        await cliente.ObtenerTicketAsync(30261);
        var trasLaPrimera = contador.Peticiones;
        await cliente.ObtenerTicketAsync(30262);

        Assert.True(contador.Peticiones > trasLaPrimera);
    }

    [Fact]
    public async Task ElEquipoTambienSeReutiliza()
    {
        var contador = new ContadorDeLlamadas();
        var cliente = Cliente(contador);

        await cliente.ObtenerEquipoAsync("Computer", 657);
        var trasLaPrimera = contador.Peticiones;
        await cliente.ObtenerEquipoAsync("Computer", 657);

        Assert.Equal(trasLaPrimera, contador.Peticiones);
    }

    private static GlpiClient Cliente(ContadorDeLlamadas contador)
    {
        var opciones = Options.Create(new GlpiOptions
        {
            BaseUrl = "http://glpi-de-prueba/apirest.php/",
            AppToken = "token-de-prueba",
            Username = "usuario",
            Password = "clave",
            TimeoutSeconds = 5
        });

        var servicios = new ServiceCollection();
        servicios.AddHttpClient(GlpiSessionProvider.HttpClientName, http =>
            http.BaseAddress = opciones.Value.BaseUriNormalizada())
            .ConfigurePrimaryHttpMessageHandler(() => contador);

        var fabrica = servicios.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var sesion = new GlpiSessionProvider(fabrica, opciones, NullLogger<GlpiSessionProvider>.Instance);

        return new GlpiClient(
            fabrica.CreateClient(GlpiSessionProvider.HttpClientName),
            sesion,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GlpiClient>.Instance);
    }

    /// <summary>GLPI de mentira que cuenta cuántas peticiones recibe.</summary>
    private sealed class ContadorDeLlamadas : HttpMessageHandler
    {
        public int Peticiones { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones++;

            var cuerpo = request.RequestUri!.AbsolutePath.EndsWith("initSession", StringComparison.Ordinal)
                ? """{"session_token":"sesion-de-prueba"}"""
                : request.RequestUri.AbsolutePath.Contains("Item_Ticket", StringComparison.Ordinal)
                    ? """[{"id":21,"itemtype":"Computer","items_id":657,"tickets_id":30261}]"""
                    : """{"id":657,"name":"LAB","serial":"MXL04916TL","manufacturers_id":"HP"}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            });
        }
    }
}

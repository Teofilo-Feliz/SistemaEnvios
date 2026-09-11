using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

public static class GlpiServiceCollectionExtensions
{
    public static IServiceCollection AddGlpi(this IServiceCollection services, IConfiguration configuration)
    {
        var opciones = configuration.GetSection(GlpiOptions.Seccion).Get<GlpiOptions>() ?? new GlpiOptions();
        opciones.Validar();

        services.Configure<GlpiOptions>(configuration.GetSection(GlpiOptions.Seccion));
        services.AddSingleton<GlpiSessionProvider>();
        services.AddScoped<IValidadorTicketGlpi, ValidadorTicketGlpi>();
        services.AddScoped<IEquipoDeTicketGlpi, EquipoDeTicketGlpi>();

        // El App-Token identifica a la aplicación y viaja en toda petición, incluida initSession;
        // el Session-Token identifica la sesión y lo pone cada llamada, porque cambia.
        void Configurar(HttpClient http)
        {
            http.BaseAddress = opciones.BaseUriNormalizada();
            http.DefaultRequestHeaders.TryAddWithoutValidation("App-Token", opciones.AppToken);
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            // Holgado a propósito: quien corta por tiempo es la estrategia de resiliencia de
            // abajo. Si este timeout fuera el más corto, mataría la petición antes de que el
            // reintento llegara a ocurrir.
            http.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSeconds * 4);
        }

        services.AddHttpClient<IGlpiClient, GlpiClient>(Configurar).AddResiliencia(opciones);
        services.AddHttpClient(GlpiSessionProvider.HttpClientName, Configurar).AddResiliencia(opciones);

        return services;
    }

    /// <summary>
    /// Reintentos y corte de circuito sobre Polly. Solo reintenta lo transitorio (5xx, 408, fallo
    /// de red): un 404 es una respuesta legítima —el item no existe— y repetirla sería gastar
    /// tres llamadas para obtener el mismo "no".
    /// </summary>
    private static void AddResiliencia(this IHttpClientBuilder builder, GlpiOptions opciones) =>
        builder.AddStandardResilienceHandler(resiliencia =>
        {
            var intento = TimeSpan.FromSeconds(opciones.TimeoutSeconds);
            resiliencia.Retry.MaxRetryAttempts = 2;
            resiliencia.AttemptTimeout.Timeout = intento;
            resiliencia.TotalRequestTimeout.Timeout = intento * 3;
            // La librería exige que la ventana de muestreo del circuito sea al menos el doble del
            // timeout de intento; con menos, lanza al configurar.
            resiliencia.CircuitBreaker.SamplingDuration = intento * 4;
        });
}

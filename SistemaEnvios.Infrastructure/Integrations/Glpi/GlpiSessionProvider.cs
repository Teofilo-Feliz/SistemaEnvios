using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <summary>
/// Guarda el session_token de GLPI en memoria y lo renueva cuando vence.
/// </summary>
/// <remarks>
/// Es singleton a propósito: abrir sesión en cada consulta le cuesta a GLPI un round-trip de
/// autenticación por llamada y deja sesiones colgando del lado del servidor. El token nunca se
/// escribe a disco ni a configuración: vive solo aquí y muere con el proceso.
/// </remarks>
public sealed class GlpiSessionProvider(
    IHttpClientFactory clientes,
    IOptions<GlpiOptions> opciones,
    ILogger<GlpiSessionProvider> logger) : IAsyncDisposable
{
    public const string HttpClientName = "glpi-session";

    private readonly GlpiOptions _opciones = opciones.Value;
    // Sin candado, N peticiones concurrentes que encuentran el token vencido abren N sesiones y
    // se pisan el token entre ellas. Con él, la primera renueva y las demás reutilizan.
    private readonly SemaphoreSlim _candado = new(1, 1);
    private string? _token;
    private DateTimeOffset _vence = DateTimeOffset.MinValue;

    public async Task<string> ObtenerTokenAsync(CancellationToken ct = default)
    {
        if (TokenVigente() is { } vigente) return vigente;

        await _candado.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Otra petición pudo renovarlo mientras esperábamos el candado.
            if (TokenVigente() is { } reciente) return reciente;
            return await AbrirSesionAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>
    /// Descarta el token cacheado. Se llama cuando GLPI responde 401: el token pudo morir antes
    /// de nuestro TTL (reinicio de GLPI, sesión cerrada desde otro lado) y la próxima consulta
    /// debe abrir una sesión nueva en vez de repetir el mismo token muerto.
    /// </summary>
    public void Invalidar()
    {
        _token = null;
        _vence = DateTimeOffset.MinValue;
    }

    private string? TokenVigente() =>
        _token is not null && DateTimeOffset.UtcNow < _vence ? _token : null;

    private async Task<string> AbrirSesionAsync(CancellationToken ct)
    {
        var http = clientes.CreateClient(HttpClientName);
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "initSession");
        var credenciales = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_opciones.Username}:{_opciones.Password}"));
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciales);

        using var respuesta = await http.SendAsync(peticion, ct).ConfigureAwait(false);
        if (!respuesta.IsSuccessStatusCode)
        {
            // El cuerpo de error de GLPI trae el código pero también puede traer datos de la
            // instancia, así que solo registramos el estado HTTP.
            logger.LogError(
                "GLPI rechazó initSession con {Estado}. Revise Glpi:AppToken, Glpi:Username y Glpi:Password.",
                (int)respuesta.StatusCode);
            throw new GlpiIntegrationException(
                $"GLPI rechazó la apertura de sesión (HTTP {(int)respuesta.StatusCode}).");
        }

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var token = LeerSessionToken(cuerpo)
            ?? throw new GlpiIntegrationException("GLPI respondió a initSession sin session_token.");

        _token = token;
        _vence = DateTimeOffset.UtcNow.AddMinutes(_opciones.SessionTtlMinutes);
        logger.LogInformation("Sesión de GLPI abierta; vigente hasta {Vence:o}.", _vence);
        return token;
    }

    private static string? LeerSessionToken(string cuerpo)
    {
        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            return json.RootElement.TryGetProperty("session_token", out var valor)
                ? valor.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cierra la sesión al apagar la app. GLPI mantiene la sesión abierta del lado del servidor
    /// hasta que expira sola, así que soltarla es cortesía con la instancia, no un trámite.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var token = _token;
        _token = null;
        _candado.Dispose();
        if (token is null) return;

        try
        {
            var http = clientes.CreateClient(HttpClientName);
            using var peticion = new HttpRequestMessage(HttpMethod.Post, "killSession");
            peticion.Headers.TryAddWithoutValidation("Session-Token", token);
            // El apagado no espera indefinidamente por un servicio externo.
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var _ = await http.SendAsync(peticion, limite.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Un killSession fallido no puede impedir que la app se apague.
            logger.LogWarning(ex, "No se pudo cerrar la sesión de GLPI durante el apagado.");
        }
    }
}

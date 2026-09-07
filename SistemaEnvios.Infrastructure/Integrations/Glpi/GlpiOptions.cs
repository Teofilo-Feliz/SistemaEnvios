namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <summary>
/// Configuración de la mesa de ayuda. Solo <see cref="BaseUrl"/> vive en appsettings.json; las
/// tres credenciales llegan por User Secrets en local y por variables de entorno en despliegue.
/// </summary>
public sealed class GlpiOptions
{
    public const string Seccion = "Glpi";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Token de la aplicación registrada en GLPI (API Client), no del usuario.</summary>
    public string AppToken { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Cuánto reutilizamos un session_token antes de pedir uno nuevo. GLPI también lo expira por
    /// su cuenta; renovarlo antes evita gastar una llamada en descubrir que ya venció.
    /// </summary>
    public int SessionTtlMinutes { get; set; } = 45;

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// GLPI resuelve rutas relativas contra apirest.php, así que la base tiene que terminar en
    /// barra: sin ella, <c>new Uri(base, "Ticket/1")</c> descarta el último segmento y la
    /// petición sale a /Ticket/1 en la raíz del sitio, que responde 404 y parece "no existe".
    /// </summary>
    public Uri BaseUriNormalizada() =>
        new(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");

    /// <summary>
    /// Falla al arrancar, como ya hace el proyecto con la cadena de conexión y los orígenes CORS:
    /// una credencial ausente descubierta en la primera consulta real es un 502 en la cara del
    /// usuario, no un problema de configuración visible.
    /// </summary>
    public void Validar()
    {
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(BaseUrl)) faltantes.Add($"{Seccion}:{nameof(BaseUrl)}");
        if (string.IsNullOrWhiteSpace(AppToken)) faltantes.Add($"{Seccion}:{nameof(AppToken)}");
        if (string.IsNullOrWhiteSpace(Username)) faltantes.Add($"{Seccion}:{nameof(Username)}");
        if (string.IsNullOrWhiteSpace(Password)) faltantes.Add($"{Seccion}:{nameof(Password)}");

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Falta configurar la integración con GLPI: {string.Join(", ", faltantes)}. " +
                "Las credenciales van en User Secrets (dotnet user-secrets set) o en variables " +
                "de entorno, nunca en appsettings.json.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"'{Seccion}:{nameof(BaseUrl)}' no es una URL absoluta válida.");

        if (SessionTtlMinutes is < 1 or > 240)
            throw new InvalidOperationException($"'{Seccion}:{nameof(SessionTtlMinutes)}' debe estar entre 1 y 240.");

        if (TimeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException($"'{Seccion}:{nameof(TimeoutSeconds)}' debe estar entre 1 y 120.");
    }
}

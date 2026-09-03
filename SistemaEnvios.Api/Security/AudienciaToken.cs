using System.Security.Claims;

namespace SistemaEnvios.Api.Security;

/// <summary>
/// AuthManager firma con la misma llave los tokens de todas las aplicaciones de la institución,
/// así que validar solo el emisor no distingue un token de LogiTrack de uno de RRHH. La
/// audiencia sí, pero su valor lo define AuthManager al registrar el cliente: por eso se
/// configura en vez de fijarse en el código, y mientras no esté configurada el API lo advierte
/// en el arranque y registra las audiencias que ve para que se sepa cuál poner.
/// </summary>
public static class AudienciaToken
{
    public const string Seccion = "Authentication:Audiences";

    public static string[] Configuradas(IConfiguration configuracion) =>
        (configuracion.GetSection(Seccion).Get<string[]>() ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .ToArray();

    public static bool EstaValidada(IConfiguration configuracion) => Configuradas(configuracion).Length > 0;

    public static IReadOnlyCollection<string> DelPrincipal(ClaimsPrincipal usuario) =>
        usuario.FindAll("aud").Select(x => x.Value)
            .Concat(usuario.FindAll(ClaimTypes.Authentication).Select(x => x.Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}

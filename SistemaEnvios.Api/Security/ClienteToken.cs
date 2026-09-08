using System.Security.Claims;

namespace SistemaEnvios.Api.Security;

/// <summary>
/// Restringe el API a los tokens emitidos para esta aplicación.
/// </summary>
/// <remarks>
/// AuthManager firma con la misma llave los tokens de todas las aplicaciones de la institución,
/// así que validar solo el emisor deja entrar el token de RRHH igual que el propio. Lo natural
/// sería validar la audiencia, pero se comprobó contra la instancia real y AuthManager
/// <b>no emite el claim "aud"</b> para este cliente: configurar 'Authentication:Audiences'
/// rechazaría todos los tokens y nadie podría entrar.
///
/// Lo que sí viaja es "client_id". Identifica a la aplicación para la que se emitió el token,
/// que es exactamente lo que hace falta distinguir.
/// </remarks>
public static class ClienteToken
{
    public const string Seccion = "Authentication:ClientIds";
    public const string Claim = "client_id";

    public static string[] Configurados(IConfiguration configuracion) =>
        (configuracion.GetSection(Seccion).Get<string[]>() ?? [])
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .ToArray();

    public static bool EstaValidado(IConfiguration configuracion) => Configurados(configuracion).Length > 0;

    /// <summary>Los client_id que trae el token. Normalmente uno.</summary>
    public static IReadOnlyCollection<string> DelPrincipal(ClaimsPrincipal usuario) =>
        usuario.FindAll(Claim)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// True si el token fue emitido para alguna de las aplicaciones admitidas. Un token sin
    /// client_id se rechaza cuando la lista está configurada: no poder comprobar de quién es un
    /// token no es razón para aceptarlo.
    /// </summary>
    public static bool Autorizado(ClaimsPrincipal usuario, string[] admitidos) =>
        DelPrincipal(usuario).Any(x => admitidos.Contains(x, StringComparer.OrdinalIgnoreCase));
}

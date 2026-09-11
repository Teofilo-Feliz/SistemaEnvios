using System.Security.Claims;

namespace SistemaEnvios.Api.Security;

/// <summary>
/// El alcance y los permisos se resuelven cruzando los ROLES del token contra las tablas de
/// acceso. Cuando ningún rol está mapeado, el usuario entra autenticado pero sin permisos y todas
/// las pantallas responden 403 sin explicar la causa.
///
/// Esto describe las claves exactas que trajo el token para poder mapearlas. Es la diferencia
/// entre saber qué fila insertar y tener que adivinar cómo escribe AuthManager cada nombre. La
/// posición se describe también aunque ya no conceda acceso: es el cargo de la persona y ayuda a
/// identificar de quién se está hablando al leer el log.
/// </summary>
public static class ClavesDeAcceso
{
    public const string ClaimPosicion = "position";
    public const string ClaimRoles = "roles";

    public static string Describir(ClaimsPrincipal principal)
    {
        var posicion = principal.FindFirst(ClaimPosicion)?.Value?.Trim();
        var roles = Roles(principal);

        var parteposicion = string.IsNullOrEmpty(posicion) ? "sin posición" : $"posición '{posicion}'";
        var parteRoles = roles.Count == 0
            ? "sin roles"
            : "roles " + string.Join(", ", roles.Select(x => $"'{x}'"));

        return $"{parteposicion}; {parteRoles}";
    }

    /// <summary>Roles del token, sin repetidos. Pueden venir en varios claims o separados por coma.</summary>
    public static IReadOnlyList<string> Roles(ClaimsPrincipal principal) =>
        [.. principal.FindAll(ClaimRoles)
            .SelectMany(x => x.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
}

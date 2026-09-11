namespace SistemaEnvios.Application.DTOs.Seguridad;

/// <summary>Quién es el usuario y qué alcance tiene, según lo decide el backend.</summary>
/// <remarks>
/// Roles es la clave con la que se resuelve el alcance: se cruza contra PerfilesPorPosicion y
/// gana el de mayor alcance. Posicion viaja al lado como dato informativo —es el cargo de
/// recursos humanos— pero ya no concede nada: no se administra desde AuthManager y por tanto no
/// se puede revocar.
///
/// Los roles se devuelven para que un problema de acceso se pueda diagnosticar desde el navegador.
/// Sin ellos, la respuesta decía a qué perfil llegó el usuario pero no con qué clave, y la única
/// forma de averiguarlo era leer el log del contenedor o decodificar el token entero. Un usuario
/// que aterriza en el módulo equivocado es el fallo más frecuente de este sistema, y era también
/// el más ciego.
///
/// No expone nada nuevo: son los mismos roles que el usuario ya lleva en su propio token.
/// </remarks>
public sealed record PerfilUsuarioResponse(
    string Perfil,
    string? Posicion,
    IReadOnlyCollection<string> Roles,
    int? FilialId,
    string? FilialNombre,
    int? UbicacionId,
    bool FilialMapeada,
    bool PuedeFiltrarPorFilial,
    IReadOnlyCollection<string> Permisos);

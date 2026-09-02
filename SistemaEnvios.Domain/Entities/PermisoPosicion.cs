namespace SistemaEnvios.Domain.Entities;

/// <summary>
/// Traduce una posición (cargo) de AuthManager a un permiso de este sistema. AuthManager
/// emite permisos de otras aplicaciones ("evaluador"), así que los propios se derivan de aquí.
/// </summary>
public class PermisoPosicion
{
    public string Posicion { get; set; } = null!;
    public string Permiso { get; set; } = null!;
}

namespace SistemaEnvios.Domain.Entities;

/// <summary>
/// Qué alcance le corresponde a una posición (cargo) de AuthManager. El claim "position"
/// es lo que distingue a Tecnología de un administrador de filial: ambos pueden compartir
/// el mismo centro, así que el alcance no puede deducirse de la ubicación.
/// </summary>
public class PerfilPosicion
{
    public string Posicion { get; set; } = null!;
    /// <summary>Corresponde a PerfilAlcance: 1 Global, 2 Transportación, 3 Filial.</summary>
    public byte Perfil { get; set; }
}

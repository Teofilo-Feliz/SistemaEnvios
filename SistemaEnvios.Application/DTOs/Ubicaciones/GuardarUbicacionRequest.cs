using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.DTOs.Ubicaciones;

public sealed class GuardarUbicacionRequest
{
    public int UbicacionId { get; init; }
    public string Nombre { get; init; } = null!;
    public string CodigoCentro { get; init; } = null!;
    public TipoUbicacionEnum Tipo { get; init; }

    /// <summary>
    /// Id de la filial en AuthManager (claim "affiliate"). Es lo que ata esta ubicación con los
    /// usuarios: el alcance por filial cruza este campo contra el claim del token. Una filial
    /// creada sin él no la alcanza nadie, y el usuario recibe "su filial no está asociada a
    /// ninguna ubicación" sin poder hacer nada al respecto.
    /// </summary>
    public int? FilialExternaId { get; init; }
}

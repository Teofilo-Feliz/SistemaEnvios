using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.DTOs.Ubicaciones;

public sealed class GuardarUbicacionRequest
{
    public int UbicacionId { get; init; }
    public string Nombre { get; init; } = null!;
    public string CodigoCentro { get; init; } = null!;
    public TipoUbicacionEnum Tipo { get; init; }
}

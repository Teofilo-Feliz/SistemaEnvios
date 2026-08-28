namespace SistemaEnvios.Application.DTOs.TiposEquipos;

public sealed class GuardarTipoEquipoRequest
{
    public int TipoEquipoId { get; init; }
    public string Nombre { get; init; } = null!;
}

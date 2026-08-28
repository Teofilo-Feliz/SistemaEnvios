namespace SistemaEnvios.Application.DTOs.Equipos;

public sealed class CrearEquipoRequest
{
    public string? CodigoActivo { get; init; }
    public string? NumeroSerie { get; init; }
    public int TipoEquipoId { get; init; }
    public int UbicacionActualId { get; init; }
    public string Marca { get; init; } = null!;
    public string Modelo { get; init; } = null!;
    public string? Observaciones { get; init; }
}

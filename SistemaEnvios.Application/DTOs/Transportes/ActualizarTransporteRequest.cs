namespace SistemaEnvios.Application.DTOs.Transportes;

public sealed class ActualizarTransporteRequest
{
    public int TransporteId { get; init; }
    public string Tipo { get; init; } = null!;
    public string? NombreChofer { get; init; }
    public string? Placa { get; init; }
    public string? Observaciones { get; init; }
}

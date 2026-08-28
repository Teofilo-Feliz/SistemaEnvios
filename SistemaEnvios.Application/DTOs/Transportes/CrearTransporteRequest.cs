namespace SistemaEnvios.Application.DTOs.Transportes;

public sealed class CrearTransporteRequest
{
    public int EnvioId { get; init; }
    public string Tipo { get; init; } = null!;
    public string? NombreChofer { get; init; }
    public string? Placa { get; init; }
    public string? Observaciones { get; init; }
}

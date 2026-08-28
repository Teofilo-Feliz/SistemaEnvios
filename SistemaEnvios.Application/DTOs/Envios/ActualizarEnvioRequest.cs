namespace SistemaEnvios.Application.DTOs.Envios;

public sealed class ActualizarEnvioRequest
{
    public int EnvioId { get; init; }
    public int UbicacionOrigenId { get; init; }
    public int UbicacionDestinoId { get; init; }
    public string? Observaciones { get; init; }
}

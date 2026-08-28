namespace SistemaEnvios.Application.DTOs.Envios;

public sealed class CrearEnvioRequest
{
    public int UbicacionOrigenId { get; init; }
    public int UbicacionDestinoId { get; init; }
    public string? Observaciones { get; init; }
}

namespace SistemaEnvios.Application.DTOs.Estados;

public sealed class CambiarEstadoEnvioRequest
{
    public int EnvioId { get; init; }
    public int EstadoDestinoId { get; init; }
    public string? Observaciones { get; init; }
}

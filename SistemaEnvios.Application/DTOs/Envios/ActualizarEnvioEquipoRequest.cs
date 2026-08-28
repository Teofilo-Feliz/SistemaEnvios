namespace SistemaEnvios.Application.DTOs.Envios;

public sealed class ActualizarEnvioEquipoRequest
{
    public int EnvioEquipoId { get; init; }
    public string NumeroTicket { get; init; } = null!;
    public string Observaciones { get; init; } = null!;
}

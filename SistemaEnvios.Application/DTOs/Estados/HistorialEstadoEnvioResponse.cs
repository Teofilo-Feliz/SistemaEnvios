namespace SistemaEnvios.Application.DTOs.Estados;

public sealed record HistorialEstadoEnvioResponse(
    int HistorialEstadoEnvioId,
    int EnvioId,
    int EstadoEnvioId,
    DateTime Fecha,
    int UbicacionId,
    Guid UsuarioId,
    string? Observaciones);

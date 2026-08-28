namespace SistemaEnvios.Application.DTOs.Estados;

public sealed record TransicionEstadoResponse(
    int TransicionEstadoEnvioId,
    int EstadoOrigenId,
    int EstadoDestinoId,
    bool Activo);

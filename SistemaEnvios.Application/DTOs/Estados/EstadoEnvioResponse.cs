namespace SistemaEnvios.Application.DTOs.Estados;

public sealed record EstadoEnvioResponse(
    int EstadoEnvioId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    bool EsFinal,
    bool Activo);

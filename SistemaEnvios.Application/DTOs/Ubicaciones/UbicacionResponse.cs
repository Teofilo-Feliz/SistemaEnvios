using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.DTOs.Ubicaciones;

public sealed record UbicacionResponse(
    int UbicacionId,
    string Nombre,
    string CodigoCentro,
    TipoUbicacionEnum Tipo,
    bool Activo);

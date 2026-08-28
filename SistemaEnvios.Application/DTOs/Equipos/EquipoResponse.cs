namespace SistemaEnvios.Application.DTOs.Equipos;

public sealed record EquipoResponse(
    int EquipoId,
    string? CodigoActivo,
    string? NumeroSerie,
    int TipoEquipoId,
    int UbicacionActualId,
    string Marca,
    string Modelo,
    string? Observaciones);

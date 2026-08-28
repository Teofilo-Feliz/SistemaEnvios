namespace SistemaEnvios.Application.DTOs.Incidencias;

public sealed record IncidenciaResponse(
    int IncidenciaId,
    int EnvioId,
    int? EnvioEquipoId,
    int? TransporteId,
    string Descripcion,
    DateTime FechaCreacion,
    Guid? UsuarioCreacionId);

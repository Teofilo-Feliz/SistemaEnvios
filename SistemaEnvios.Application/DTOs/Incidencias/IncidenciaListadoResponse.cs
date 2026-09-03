namespace SistemaEnvios.Application.DTOs.Incidencias;

/// <summary>
/// Fila del listado general. Trae el número de envío ya resuelto para que la pantalla no tenga
/// que cruzarlo contra una lista de envíos traída aparte.
/// </summary>
public sealed record IncidenciaListadoResponse(
    int IncidenciaId,
    int EnvioId,
    string NumeroEnvio,
    int? EnvioEquipoId,
    int? TransporteId,
    string Descripcion,
    DateTime FechaCreacion,
    Guid? UsuarioCreacionId);

using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Application.DTOs.Incidencias;

/// <summary>
/// Listado general de incidencias. Existe para que la pantalla no tenga que pedir todos los
/// envíos y luego una consulta de incidencias por cada uno: eso eran cientos de peticiones para
/// llenar una sola tabla.
/// </summary>
public sealed class ConsultarIncidenciasRequest : ParametrosPagina
{
    /// <summary>Busca en la descripción o en el número del envío.</summary>
    public string? Search { get; init; }
    public int? EnvioId { get; init; }
}

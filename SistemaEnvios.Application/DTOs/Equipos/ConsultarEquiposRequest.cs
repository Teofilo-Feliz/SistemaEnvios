using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Application.DTOs.Equipos;

/// <summary>
/// Filtro del inventario. Los criterios se aplican dentro de la consulta y no sobre una lista ya
/// traída: el inventario es 34 filiales por todos sus equipos, y filtrar en el navegador implica
/// haberlo descargado completo primero.
/// </summary>
public sealed class ConsultarEquiposRequest : ParametrosPagina
{
    /// <summary>Busca por serial, código de activo, marca o modelo.</summary>
    public string? Search { get; init; }
    public int? TipoEquipoId { get; init; }
    public int? UbicacionActualId { get; init; }
}

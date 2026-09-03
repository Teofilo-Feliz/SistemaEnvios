namespace SistemaEnvios.Application.DTOs.Common;

/// <summary>
/// Filtro común de los catálogos. Aunque hoy sean tablas cortas, se consultan paginadas: el día
/// que un catálogo crezca no hay que descubrirlo por una consulta que trajo todo.
/// </summary>
public sealed class ConsultarCatalogoRequest : ParametrosPagina
{
    public bool SoloActivos { get; init; } = true;
    public string? Search { get; init; }
}

using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Application.DTOs.Casos;

/// <summary>
/// Un equipo que hoy está en Tecnología esperando volver a su filial.
/// </summary>
/// <remarks>
/// Solo equipos con caso abierto. Lo que está en Tecnología sin caso —un descarte, un equipo que
/// todavía no se ha asignado— no sale por aquí: se asigna por otra vía, y mezclarlo en este
/// desplegable obligaba a distinguir a ojo entre lo que una filial está esperando y lo que no es
/// de nadie.
///
/// El nombre de la filial no viaja: el formulario ya tiene cargada la lista de ubicaciones para
/// el desplegable de destino y lo resuelve de ahí.
/// </remarks>
public sealed record EquipoEnTecnologiaResponse(
    int EquipoId,
    string? NumeroSerie,
    string? CodigoActivo,
    string Marca,
    string Modelo,
    int TipoEquipoId,
    string NumeroTicket,
    int FilialId);

/// <summary>
/// Filtro del inventario de Tecnología para armar un envío.
/// </summary>
/// <remarks>
/// Sin <see cref="FilialId"/> devuelve lo de todas las filiales, que es lo que se ve mientras
/// nadie ha elegido destino todavía.
/// </remarks>
public sealed class ConsultarEquiposEnTecnologiaRequest : ParametrosPagina
{
    /// <summary>Busca por serial, código de activo, marca, modelo o número de ticket.</summary>
    public string? Search { get; init; }

    public int? FilialId { get; init; }
}

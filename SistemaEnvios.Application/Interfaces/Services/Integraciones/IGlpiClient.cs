using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;

namespace SistemaEnvios.Application.Interfaces.Services.Integraciones;

/// <summary>
/// Consulta la mesa de ayuda (GLPI) para confirmar que un ticket o un activo existe antes de
/// dejar que una operación lo referencie.
/// </summary>
public interface IGlpiClient
{
    /// <summary>
    /// Si el ticket existe, en qué estado está y qué activos tiene asociados.
    /// </summary>
    /// <remarks>
    /// Son dos consultas a GLPI hechas a la vez, porque la información está repartida: el estado
    /// vive en el ticket y los activos en Item_Ticket. La existencia la decide la primera.
    ///
    /// Mismo criterio que el resto del cliente: 404 es una respuesta y sale como éxito con
    /// <see cref="TicketGlpi.Existe"/> en false; una caída sale como
    /// <see cref="ErrorType.ExternalService"/>.
    /// </remarks>
    Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default);

    /// <summary>
    /// Los datos de un activo. Devuelve null cuando GLPI responde 404.
    /// </summary>
    /// <remarks>
    /// El itemType lo dicta <see cref="ItemDeTicketGlpi.ItemType"/>: la misma llamada sirve para
    /// Computer, Monitor y Printer, y por eso monitores e impresoras no necesitan código aparte.
    /// </remarks>
    Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default);
}

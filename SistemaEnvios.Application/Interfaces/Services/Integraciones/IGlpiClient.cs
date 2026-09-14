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
    /// Los activos asociados a un ticket, y de paso si el ticket existe.
    /// </summary>
    /// <remarks>
    /// Una sola llamada responde las dos cosas: GLPI devuelve 404 cuando el ticket no existe y la
    /// lista —que puede venir vacía— cuando sí. Por eso sustituye a
    /// la consulta de existencia que había antes, en vez de sumarse a ella.
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

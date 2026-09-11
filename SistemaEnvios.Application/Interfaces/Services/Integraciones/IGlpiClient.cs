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
    /// True si GLPI tiene el item, false si respondió 404.
    /// </summary>
    /// <remarks>
    /// Devuelve <see cref="Result{T}"/> y no un <c>bool</c> pelado a propósito: con un bool, una
    /// caída de GLPI se vuelve indistinguible de "el ticket no existe", y el llamador terminaría
    /// rechazando tickets válidos porque la mesa de ayuda estaba lenta. Un fallo de la integración
    /// sale como <see cref="ErrorType.ExternalService"/> para que el API responda 502 y quien
    /// llamó sepa que reintentar tiene sentido.
    /// </remarks>
    Task<Result<bool>> ItemExistsAsync(string itemType, int id, CancellationToken ct = default);

    /// <summary>
    /// Los activos asociados a un ticket, y de paso si el ticket existe.
    /// </summary>
    /// <remarks>
    /// Una sola llamada responde las dos cosas: GLPI devuelve 404 cuando el ticket no existe y la
    /// lista —que puede venir vacía— cuando sí. Por eso sustituye a
    /// <see cref="ItemExistsAsync"/> para tickets en vez de sumarse a ella.
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

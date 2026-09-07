using SistemaEnvios.Application.Common;

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
}

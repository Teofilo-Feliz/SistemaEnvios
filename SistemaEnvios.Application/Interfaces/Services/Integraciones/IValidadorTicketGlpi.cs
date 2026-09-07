using SistemaEnvios.Application.Common;

namespace SistemaEnvios.Application.Interfaces.Services.Integraciones;

/// <summary>
/// Comprueba contra la mesa de ayuda que el ticket que escribió el usuario existe de verdad.
/// </summary>
/// <remarks>
/// Solo se le pasan tickets escritos por el usuario. Los que hereda un caso abierto salen de
/// nuestra propia base, ya se validaron cuando se abrió el caso, y volver a consultarlos
/// bloquearía la continuación de un caso legítimo si el ticket se depuró en GLPI.
/// </remarks>
public interface IValidadorTicketGlpi
{
    /// <summary>
    /// Falla con <see cref="ErrorType.Validation"/> solo cuando GLPI respondió y dijo que el
    /// ticket no existe. Si GLPI no contesta, deja pasar y registra el aviso: una caída de la
    /// mesa de ayuda no puede detener la creación de envíos en todas las filiales.
    /// </summary>
    Task<Result> ValidarAsync(string numeroTicket, CancellationToken ct = default);
}

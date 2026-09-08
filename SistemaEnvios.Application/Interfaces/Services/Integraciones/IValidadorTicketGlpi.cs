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

    /// <summary>
    /// Valida varios tickets a la vez y devuelve el primer rechazo.
    /// </summary>
    /// <remarks>
    /// Existe porque en serie no cabían: cada consulta admite hasta 45 s entre reintentos y el
    /// navegador abandona a los 20 s. Con tres tickets el usuario veía un error mientras el
    /// servidor seguía trabajando y acababa creando el envío, así que al reintentar lo duplicaba
    /// o chocaba con "el ticket ya fue utilizado". Aquí van en paralelo y bajo un tope común.
    /// </remarks>
    Task<Result> ValidarVariosAsync(IReadOnlyCollection<string> numerosTicket, CancellationToken ct = default);
}

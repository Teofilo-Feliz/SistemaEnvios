using SistemaEnvios.Application.Common;

namespace SistemaEnvios.Application.Interfaces.Services.Integraciones;

/// <summary>
/// Comprueba contra la mesa de ayuda que el ticket que escribió el usuario es usable: que existe
/// y que no arrastra más de un equipo.
/// </summary>
/// <remarks>
/// Solo se le pasan tickets escritos por el usuario. Los que hereda un caso abierto salen de
/// nuestra propia base, ya se validaron cuando se abrió el caso, y volver a consultarlos
/// bloquearía la continuación de un caso legítimo si el ticket se depuró en GLPI.
/// </remarks>
public interface IValidadorTicketGlpi
{
    /// <summary>
    /// Falla con <see cref="ErrorType.Validation"/> cuando GLPI respondió y el ticket no existe,
    /// o cuando trae más de un equipo asociado. Si GLPI no contesta, deja pasar y registra el
    /// aviso: una caída de la mesa de ayuda no puede detener la creación de envíos en todas las
    /// filiales, y ese es también el único hueco de la regla de un equipo por ticket.
    /// </summary>
    /// <remarks>
    /// Un ticket sin equipos SÍ pasa: es el caso de quien abre el ticket y todavía no le colgó el
    /// activo, y la persona escribe marca, modelo y serial a mano.
    /// </remarks>
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

    /// <summary>
    /// Además de que el ticket sea usable, confirma que en la mesa de ayuda corresponda al equipo
    /// del serial indicado.
    /// </summary>
    /// <remarks>
    /// Se usa al corregir el ticket de una fila que ya existe. Sin esto se le podía poner a un
    /// equipo el ticket de otro y la fila quedaba diciendo que el caso de una máquina es el de
    /// aquella. Cambiar la fila al equipo del ticket nuevo no es alternativa: esos datos son del
    /// registro del inventario y se sobrescribiría la ficha de una máquina con la de otra.
    ///
    /// Solo rechaza la contradicción demostrada: que el ticket sea de OTRA máquina. Un ticket sin
    /// activo asociado pasa, porque es el caso legítimo de quien abre el ticket antes de colgarle
    /// el equipo y escribe los datos a mano. GLPI caído también deja pasar, como en el resto.
    /// </remarks>
    Task<Result> ValidarParaEquipoAsync(string numeroTicket, string? numeroSerie, CancellationToken ct = default);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Integraciones;

/// <summary>
/// Lo único que este sistema le pregunta a la mesa de ayuda.
/// </summary>
/// <remarks>
/// Aquí hubo rutas de existencia, incluida una comodín /{itemType}/{id}/existe. Con solo
/// 'envios.consultar' —que tienen todos los perfiles— cualquiera podía preguntar por User,
/// Contract o Supplier y enumerar la instancia interna de GLPI por id. Se redujo a dos rutas
/// explícitas y, cuando Item_Ticket pasó a responder también si el ticket existe, dejaron de
/// tener consumidor.
/// </remarks>
[ApiController]
[Route("api/glpi")]
public sealed class GlpiController(IEquipoDeTicketGlpi equipos) : ControllerBase
{
    /// <summary>
    /// El equipo del ticket, para autocompletar el formulario. Devuelve el equipo en null cuando
    /// el ticket no tiene ninguno asociado, y falla con el mismo mensaje que daría al guardar
    /// cuando el ticket no existe o trae más de uno.
    /// </summary>
    [HttpGet("ticket/{numeroTicket}/equipo")]
    [Authorize(Policy = PermissionNames.EnviosCrear)]
    public async Task<IActionResult> EquipoDelTicket(string numeroTicket, CancellationToken ct)
    {
        var resultado = await equipos.ObtenerAsync(numeroTicket, ct);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(this);
    }
}

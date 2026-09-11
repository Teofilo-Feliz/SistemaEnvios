using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Contracts.Integraciones;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Integraciones;

[ApiController]
[Route("api/glpi")]
public sealed class GlpiController(IGlpiClient glpi, IEquipoDeTicketGlpi equipos) : ControllerBase
{
    [HttpGet("ticket/{id:int}/existe")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public Task<IActionResult> TicketExiste(int id, CancellationToken ct) =>
        ExisteAsync("Ticket", id, ct);

    [HttpGet("computer/{id:int}/existe")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public Task<IActionResult> ComputadoraExiste(int id, CancellationToken ct) =>
        ExisteAsync("Computer", id, ct);

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

    // Aquí había una ruta comodín /{itemType}/{id}/existe. Con solo 'envios.consultar' —que
    // tienen todos los perfiles— cualquiera podía preguntar por User, Contract, Supplier o
    // Problem y enumerar la instancia interna de GLPI por id, además de consumir la sesión y el
    // cortacircuitos que usa la validación real de tickets. El frontend solo llama a 'ticket';
    // las dos rutas explícitas cubren lo que el sistema necesita.

    private async Task<IActionResult> ExisteAsync(string itemType, int id, CancellationToken ct)
    {
        var resultado = await glpi.ItemExistsAsync(itemType, id, ct);
        
        return resultado.IsSuccess
            ? Ok(new GlpiExistenciaResponse(itemType, id, resultado.Value))
            : resultado.ToActionResult(this);
    }
}

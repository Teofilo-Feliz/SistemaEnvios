using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Contracts.Integraciones;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Integraciones;

[ApiController]
[Route("api/glpi")]
public sealed class GlpiController(IGlpiClient glpi) : ControllerBase
{
    [HttpGet("ticket/{id:int}/existe")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public Task<IActionResult> TicketExiste(int id, CancellationToken ct) =>
        ExisteAsync("Ticket", id, ct);

    [HttpGet("computer/{id:int}/existe")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public Task<IActionResult> ComputadoraExiste(int id, CancellationToken ct) =>
        ExisteAsync("Computer", id, ct);

    
    [HttpGet("{itemType}/{id:int}/existe")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public Task<IActionResult> ItemExiste(string itemType, int id, CancellationToken ct) =>
        ExisteAsync(itemType, id, ct);

    private async Task<IActionResult> ExisteAsync(string itemType, int id, CancellationToken ct)
    {
        var resultado = await glpi.ItemExistsAsync(itemType, id, ct);
        
        return resultado.IsSuccess
            ? Ok(new GlpiExistenciaResponse(itemType, id, resultado.Value))
            : resultado.ToActionResult(this);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Envios;

[ApiController]
[Route("api/envio-equipos")]
public sealed class EnvioEquiposController(IEnvioEquipoService service) : ControllerBase
{
    [HttpGet("por-envio/{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> ListarPorEnvio(int envioId, CancellationToken cancellationToken) =>
        (await service.ListarPorEnvioAsync(envioId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.EnviosEditar)]
    public async Task<IActionResult> Agregar(
        [FromBody] AgregarEquipoEnvioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AgregarAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ListarPorEnvio), new { envioId = request.EnvioId }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPut("{envioEquipoId:int}")]
    [Authorize(Policy = PermissionNames.EnviosEditar)]
    public async Task<IActionResult> Actualizar(
        int envioEquipoId,
        [FromBody] ActualizarEnvioEquipoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = envioEquipoId,
            NumeroTicket = request.NumeroTicket,
            Observaciones = request.Observaciones
        };
        return (await service.ActualizarAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpDelete("{envioEquipoId:int}")]
    [Authorize(Policy = PermissionNames.EnviosEditar)]
    public async Task<IActionResult> Quitar(int envioEquipoId, CancellationToken cancellationToken) =>
        (await service.QuitarAsync(envioEquipoId, cancellationToken)).ToActionResult(this);
}

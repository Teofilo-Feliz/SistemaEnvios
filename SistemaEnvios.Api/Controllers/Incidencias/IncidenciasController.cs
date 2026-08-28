using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Incidencias;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Incidencias;

[ApiController]
[Route("api/incidencias")]
public sealed class IncidenciasController(IIncidenciaService service) : ControllerBase
{
    [HttpGet("{incidenciaId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Obtener(int incidenciaId, CancellationToken cancellationToken) =>
        (await service.ObtenerAsync(incidenciaId, cancellationToken)).ToActionResult(this);

    [HttpGet("por-envio/{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> ListarPorEnvio(int envioId, CancellationToken cancellationToken) =>
        (await service.ListarPorEnvioAsync(envioId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.IncidenciasGestionar)]
    public async Task<IActionResult> Registrar(
        [FromBody] CrearIncidenciaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RegistrarAsync(request, cancellationToken);
        return result.ToCreatedAtActionResult(this, nameof(Obtener), new { incidenciaId = result.Value });
    }
}

using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Contracts.Catalogos;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.TiposEquipos;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Catalogos;

[ApiController]
[Route("api/tipos-equipos")]
public sealed class TiposEquiposController(ITipoEquipoService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultarCatalogoRequest request,
        CancellationToken cancellationToken = default) =>
        (await service.ListarAsync(request, cancellationToken)).ToActionResult(this);

    [HttpGet("{tipoEquipoId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Obtener(int tipoEquipoId, CancellationToken cancellationToken) =>
        (await service.ObtenerAsync(tipoEquipoId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.CatalogosAdministrar)]
    public async Task<IActionResult> Crear(
        [FromBody] GuardarTipoEquipoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CrearAsync(request, cancellationToken);
        return result.ToCreatedAtActionResult(this, nameof(Obtener), new { tipoEquipoId = result.Value });
    }

    [HttpPut("{tipoEquipoId:int}")]
    [Authorize(Policy = PermissionNames.CatalogosAdministrar)]
    public async Task<IActionResult> Actualizar(
        int tipoEquipoId,
        [FromBody] GuardarTipoEquipoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarTipoEquipoRequest
        {
            TipoEquipoId = tipoEquipoId,
            Nombre = request.Nombre
        };
        return (await service.ActualizarAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpPatch("{tipoEquipoId:int}/activo")]
    [Authorize(Policy = PermissionNames.CatalogosAdministrar)]
    public async Task<IActionResult> CambiarActivo(
        int tipoEquipoId,
        [FromBody] CambiarActivoRequest request,
        CancellationToken cancellationToken) =>
        (await service.CambiarActivoAsync(tipoEquipoId, request.Activo, cancellationToken)).ToActionResult(this);
}

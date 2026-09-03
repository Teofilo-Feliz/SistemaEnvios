using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Contracts.Catalogos;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Catalogos;

[ApiController]
[Route("api/ubicaciones")]
public sealed class UbicacionesController(IUbicacionService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultarCatalogoRequest request,
        CancellationToken cancellationToken = default) =>
        (await service.ListarAsync(request, cancellationToken)).ToActionResult(this);

    [HttpGet("{ubicacionId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Obtener(int ubicacionId, CancellationToken cancellationToken) =>
        (await service.ObtenerAsync(ubicacionId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.CatalogosAdministrar)]
    public async Task<IActionResult> Crear(
        [FromBody] GuardarUbicacionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CrearAsync(request, cancellationToken);
        return result.ToCreatedAtActionResult(this, nameof(Obtener), new { ubicacionId = result.Value });
    }

    [HttpPut("{ubicacionId:int}")]
    [Authorize(Policy = PermissionNames.CatalogosAdministrar)]
    public async Task<IActionResult> Actualizar(
        int ubicacionId,
        [FromBody] GuardarUbicacionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarUbicacionRequest
        {
            UbicacionId = ubicacionId,
            Nombre = request.Nombre,
            CodigoCentro = request.CodigoCentro,
            Tipo = request.Tipo
        };
        return (await service.ActualizarAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpPatch("{ubicacionId:int}/activo")]
    [Authorize(Policy = PermissionNames.CatalogosAdministrar)]
    public async Task<IActionResult> CambiarActivo(
        int ubicacionId,
        [FromBody] CambiarActivoRequest request,
        CancellationToken cancellationToken) =>
        (await service.CambiarActivoAsync(ubicacionId, request.Activo, cancellationToken)).ToActionResult(this);
}

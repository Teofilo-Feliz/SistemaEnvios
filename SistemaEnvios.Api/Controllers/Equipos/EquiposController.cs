using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Equipos;

[ApiController]
[Route("api/equipos")]
public sealed class EquiposController(IEquipoService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        (await service.ListarAsync(cancellationToken)).ToActionResult(this);

    [HttpGet("{equipoId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Obtener(int equipoId, CancellationToken cancellationToken) =>
        (await service.ObtenerAsync(equipoId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.EquiposGestionar)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearEquipoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CrearAsync(request, cancellationToken);
        return result.ToCreatedAtActionResult(this, nameof(Obtener), new { equipoId = result.Value });
    }

    [HttpPut("{equipoId:int}")]
    [Authorize(Policy = PermissionNames.EquiposGestionar)]
    public async Task<IActionResult> Actualizar(
        int equipoId,
        [FromBody] ActualizarEquipoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarEquipoRequest
        {
            EquipoId = equipoId,
            CodigoActivo = request.CodigoActivo,
            NumeroSerie = request.NumeroSerie,
            TipoEquipoId = request.TipoEquipoId,
            UbicacionActualId = request.UbicacionActualId,
            Marca = request.Marca,
            Modelo = request.Modelo,
            Observaciones = request.Observaciones
        };
        return (await service.ActualizarAsync(command, cancellationToken)).ToActionResult(this);
    }
}

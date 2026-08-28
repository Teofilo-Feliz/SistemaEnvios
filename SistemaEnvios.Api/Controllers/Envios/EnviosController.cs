using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Envios;

[ApiController]
[Route("api/envios")]
public sealed class EnviosController(
    IEnvioService envioService,
    IEstadoEnvioService estadoService,
    IHistorialEstadoEnvioService historialService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        (await envioService.ListarAsync(cancellationToken)).ToActionResult(this);

    [HttpGet("{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Obtener(int envioId, CancellationToken cancellationToken) =>
        (await envioService.ObtenerAsync(envioId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.EnviosCrear)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearEnvioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await envioService.CrearAsync(request, cancellationToken);
        return result.ToCreatedAtActionResult(this, nameof(Obtener), new { envioId = result.Value?.EnvioId });
    }

    [HttpPut("{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosEditar)]
    public async Task<IActionResult> Actualizar(
        int envioId,
        [FromBody] ActualizarEnvioRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarEnvioRequest
        {
            EnvioId = envioId,
            UbicacionOrigenId = request.UbicacionOrigenId,
            UbicacionDestinoId = request.UbicacionDestinoId,
            Observaciones = request.Observaciones
        };
        return (await envioService.ActualizarAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpPost("{envioId:int}/estado")]
    [Authorize(Policy = PermissionNames.EnviosDespachar)]
    public async Task<IActionResult> CambiarEstado(
        int envioId,
        [FromBody] CambiarEstadoEnvioRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CambiarEstadoEnvioRequest
        {
            EnvioId = envioId,
            EstadoDestinoId = request.EstadoDestinoId,
            Observaciones = request.Observaciones
        };
        return (await estadoService.CambiarAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpGet("{envioId:int}/historial")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> ListarHistorial(int envioId, CancellationToken cancellationToken) =>
        (await historialService.ListarPorEnvioAsync(envioId, cancellationToken)).ToActionResult(this);
}

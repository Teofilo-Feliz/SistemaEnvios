using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Transportes;

[ApiController]
[Route("api/transportes")]
public sealed class TransportesController(ITransporteService service) : ControllerBase
{
    [HttpGet("por-envio/{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> ObtenerPorEnvio(int envioId, CancellationToken cancellationToken) =>
        (await service.ObtenerPorEnvioAsync(envioId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.TransportesGestionar)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearTransporteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CrearAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ObtenerPorEnvio), new { envioId = request.EnvioId }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPut("{transporteId:int}")]
    [Authorize(Policy = PermissionNames.TransportesGestionar)]
    public async Task<IActionResult> Actualizar(
        int transporteId,
        [FromBody] ActualizarTransporteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarTransporteRequest
        {
            TransporteId = transporteId,
            Tipo = request.Tipo,
            NombreChofer = request.NombreChofer,
            Placa = request.Placa,
            Observaciones = request.Observaciones
        };
        return (await service.ActualizarAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpPost("{transporteId:int}/confirmacion")]
    [Authorize(Policy = PermissionNames.TransportesConfirmar)]
    public async Task<IActionResult> Confirmar(int transporteId, CancellationToken cancellationToken) =>
        (await service.ConfirmarAsync(transporteId, cancellationToken)).ToActionResult(this);
}

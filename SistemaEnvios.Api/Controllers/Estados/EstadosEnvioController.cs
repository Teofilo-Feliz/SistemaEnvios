using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Estados;

[ApiController]
[Route("api/estados-envio")]
[Authorize(Policy = PermissionNames.EnviosConsultar)]
public sealed class EstadosEnvioController(
    IEstadoEnvioService estadoService,
    ITransicionEstadoEnvioService transicionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] ConsultarCatalogoRequest request,
        CancellationToken cancellationToken = default) =>
        (await estadoService.ListarAsync(request, cancellationToken)).ToActionResult(this);

    [HttpGet("{estadoId:int}")]
    public async Task<IActionResult> Obtener(int estadoId, CancellationToken cancellationToken) =>
        (await estadoService.ObtenerAsync(estadoId, cancellationToken)).ToActionResult(this);

    [HttpGet("transiciones")]
    public async Task<IActionResult> ListarTransiciones(
        [FromQuery] ConsultarCatalogoRequest request,
        CancellationToken cancellationToken = default) =>
        (await transicionService.ListarAsync(request, cancellationToken)).ToActionResult(this);

    [HttpGet("transiciones/{transicionId:int}")]
    public async Task<IActionResult> ObtenerTransicion(int transicionId, CancellationToken cancellationToken) =>
        (await transicionService.ObtenerAsync(transicionId, cancellationToken)).ToActionResult(this);
}

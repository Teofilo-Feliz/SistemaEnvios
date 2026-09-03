using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Casos;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Casos;

/// <summary>
/// Casos abiertos y descarte. Descartar es lo que cierra un caso que no va a terminar con el
/// equipo de vuelta en su filial, y la única vía para poder reasignarlo a otra.
/// </summary>
[ApiController]
[Route("api/casos")]
public sealed class CasosController(ICasoEquipoService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Listar([FromQuery] ConsultarCasosRequest request, CancellationToken cancellationToken) =>
        (await service.ListarAbiertosEnTecnologiaAsync(request, cancellationToken)).ToActionResult(this);

    /// <summary>El caso abierto de un equipo: de aquí sale el ticket que hereda el envío.</summary>
    [HttpGet("equipo/{equipoId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> DeEquipo(int equipoId, CancellationToken cancellationToken) =>
        (await service.ConsultarDeEquipoAsync(equipoId, cancellationToken)).ToActionResult(this);

    [HttpPost("descartar")]
    [Authorize(Policy = PermissionNames.EquiposGestionar)]
    public async Task<IActionResult> Descartar([FromBody] DescartarEquipoRequest request, CancellationToken cancellationToken) =>
        (await service.DescartarAsync(request.EquipoId, request.Motivo, cancellationToken)).ToActionResult(this);
}

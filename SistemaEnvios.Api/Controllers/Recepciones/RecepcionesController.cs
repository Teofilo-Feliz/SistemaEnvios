using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Recepciones;

[ApiController]
[Route("api/recepciones")]
public sealed class RecepcionesController(IRecepcionService service) : ControllerBase
{
    /// <summary>Los equipos que llegaron mal, para poder ver cuáles son y qué les pasa.</summary>
    [HttpGet("incidencias/por-envio/{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> IncidenciasPorEnvio(int envioId, [FromQuery] ParametrosPaginaSimple request, CancellationToken cancellationToken) =>
        (await service.ListarIncidenciasPorEnvioAsync(envioId, request, cancellationToken)).ToActionResult(this);

    [HttpGet("por-envio/{envioId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> ObtenerPorEnvio(int envioId, CancellationToken cancellationToken) =>
        (await service.ObtenerPorEnvioAsync(envioId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = PermissionNames.RecepcionesGestionar)]
    public async Task<IActionResult> Crear(
        [FromBody] CrearRecepcionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CrearAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ObtenerPorEnvio), new { envioId = request.EnvioId }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPut("{recepcionId:int}/tecnico")]
    [Authorize(Policy = PermissionNames.RecepcionesGestionar)]
    public async Task<IActionResult> AsignarTecnico(
        int recepcionId,
        [FromBody] AsignarTecnicoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AsignarTecnicoRequest
        {
            RecepcionId = recepcionId,
            TecnicoAsignadoUsuarioId = request.TecnicoAsignadoUsuarioId
        };
        return (await service.AsignarTecnicoAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpPost("{recepcionId:int}/equipos/verificacion")]
    [Authorize(Policy = PermissionNames.RecepcionesGestionar)]
    public async Task<IActionResult> VerificarEquipo(
        int recepcionId,
        [FromBody] VerificarEquipoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new VerificarEquipoRequest
        {
            RecepcionId = recepcionId,
            EnvioEquipoId = request.EnvioEquipoId,
            Estado = request.Estado,
            Observaciones = request.Observaciones
        };
        return (await service.VerificarEquipoAsync(command, cancellationToken)).ToActionResult(this);
    }

    [HttpPost("{recepcionId:int}/finalizacion")]
    [Authorize(Policy = PermissionNames.RecepcionesGestionar)]
    public async Task<IActionResult> Completar(int recepcionId, CancellationToken cancellationToken) =>
        (await service.CompletarAsync(recepcionId, cancellationToken)).ToActionResult(this);
}

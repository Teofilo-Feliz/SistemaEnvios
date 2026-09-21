using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Common;
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
    public async Task<IActionResult> Listar([FromQuery] ConsultarEquiposRequest request, CancellationToken cancellationToken) =>
        (await service.ListarAsync(request, cancellationToken)).ToActionResult(this);

    [HttpGet("{equipoId:int}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Obtener(int equipoId, CancellationToken cancellationToken) =>
        (await service.ObtenerAsync(equipoId, cancellationToken)).ToActionResult(this);

    /// <summary>
    /// Por dónde ha pasado el equipo. Mismo permiso que ver la ficha: quien puede abrir el
    /// equipo puede ver su recorrido, y el alcance ya lo aplica el servicio.
    /// </summary>
    /// <summary>
    /// Si el código de activo está libre. Pide solo envios.consultar porque se usa mientras se
    /// arma un envío, y responde un booleano: no expone de qué equipo es.
    /// </summary>
    [HttpGet("codigo-activo-disponible/{codigoActivo}")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> CodigoActivoDisponible(
        string codigoActivo, [FromQuery] int? excluirEquipoId, CancellationToken cancellationToken) =>
        (await service.CodigoActivoDisponibleAsync(codigoActivo, excluirEquipoId, cancellationToken))
            .ToActionResult(this);

    [HttpGet("{equipoId:int}/historial")]
    [Authorize(Policy = PermissionNames.EnviosConsultar)]
    public async Task<IActionResult> Historial(int equipoId, [FromQuery] ParametrosPaginaSimple request, CancellationToken cancellationToken) =>
        (await service.ListarViajesAsync(equipoId, request, cancellationToken)).ToActionResult(this);

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

using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Contracts.Catalogos;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Catalogos;

[ApiController]
[Authorize(Policy = PermissionNames.EnviosConsultar)]
/// <summary>
/// La flota es de Transportación: ella mantiene los tipos de transporte y los choferes
/// internos. Leer sigue abierto a quien consulta envíos, porque el detalle de un envío
/// muestra su transporte y su chofer.
/// </summary>
public sealed class TransportesCatalogosController(ICatalogoTransporteService service) : ControllerBase
{
    [HttpGet("api/tipos-transporte")]
    public async Task<IActionResult> Tipos([FromQuery] ConsultarCatalogoRequest request,CancellationToken ct=default)=>(await service.ListarTiposAsync(request,ct)).ToActionResult(this);
    [HttpPost("api/tipos-transporte")][Authorize(Policy=PermissionNames.TransportesAdministrar)]
    public async Task<IActionResult> CrearTipo([FromBody] GuardarTipoTransporteRequest r,CancellationToken ct){var x=await service.CrearTipoAsync(r,ct);return x.IsSuccess?StatusCode(201,x.Value):x.ToActionResult(this);}
    [HttpPut("api/tipos-transporte/{id:int}")][Authorize(Policy=PermissionNames.TransportesAdministrar)]
    public async Task<IActionResult> ActualizarTipo(int id,[FromBody] GuardarTipoTransporteRequest r,CancellationToken ct)=>(await service.ActualizarTipoAsync(new(){TipoTransporteId=id,Codigo=r.Codigo,Nombre=r.Nombre,Estrategia=r.Estrategia},ct)).ToActionResult(this);
    [HttpPatch("api/tipos-transporte/{id:int}/activo")][Authorize(Policy=PermissionNames.TransportesAdministrar)]
    public async Task<IActionResult> TipoActivo(int id,[FromBody] CambiarActivoRequest r,CancellationToken ct)=>(await service.CambiarTipoActivoAsync(id,r.Activo,ct)).ToActionResult(this);
    [HttpGet("api/choferes-internos")]
    public async Task<IActionResult> Choferes([FromQuery] ConsultarCatalogoRequest request,CancellationToken ct=default)=>(await service.ListarChoferesAsync(request,ct)).ToActionResult(this);
    [HttpPost("api/choferes-internos")][Authorize(Policy=PermissionNames.TransportesAdministrar)]
    public async Task<IActionResult> CrearChofer([FromBody] GuardarChoferInternoRequest r,CancellationToken ct){var x=await service.CrearChoferAsync(r,ct);return x.IsSuccess?StatusCode(201,x.Value):x.ToActionResult(this);}
    [HttpPut("api/choferes-internos/{id:int}")][Authorize(Policy=PermissionNames.TransportesAdministrar)]
    public async Task<IActionResult> ActualizarChofer(int id,[FromBody] GuardarChoferInternoRequest r,CancellationToken ct)=>(await service.ActualizarChoferAsync(new(){ChoferInternoId=id,NombreCompleto=r.NombreCompleto,NumeroEmpleado=r.NumeroEmpleado},ct)).ToActionResult(this);
    [HttpPatch("api/choferes-internos/{id:int}/activo")][Authorize(Policy=PermissionNames.TransportesAdministrar)]
    public async Task<IActionResult> ChoferActivo(int id,[FromBody] CambiarActivoRequest r,CancellationToken ct)=>(await service.CambiarChoferActivoAsync(id,r.Activo,ct)).ToActionResult(this);
}

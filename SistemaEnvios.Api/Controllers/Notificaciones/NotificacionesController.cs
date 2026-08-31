using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;
namespace SistemaEnvios.Api.Controllers.Notificaciones;
[ApiController][Route("api/notificaciones")]
public sealed class NotificacionesController(INotificacionService service):ControllerBase
{
    [HttpGet][Authorize(Policy=PermissionNames.EnviosConsultar)] public async Task<IActionResult> Listar([FromQuery]string rol="TECNOLOGIA",[FromQuery]bool soloNoLeidas=true,CancellationToken ct=default)=>(await service.ListarAsync(rol,soloNoLeidas,ct)).ToActionResult(this);
    [HttpPatch("{id:long}/leida")][Authorize(Policy=PermissionNames.RecepcionesGestionar)] public async Task<IActionResult> MarcarLeida(long id,CancellationToken ct)=>(await service.MarcarLeidaAsync(id,ct)).ToActionResult(this);
}

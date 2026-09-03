using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.DTOs.Notificaciones;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Security;
namespace SistemaEnvios.Api.Controllers.Notificaciones;
[ApiController][Route("api/notificaciones")]
public sealed class NotificacionesController(INotificacionService service):ControllerBase
{
    [HttpGet][Authorize(Policy=PermissionNames.EnviosConsultar)] public async Task<IActionResult> Listar([FromQuery]ConsultarNotificacionesRequest request,CancellationToken ct=default)=>(await service.ListarAsync(request,ct)).ToActionResult(this);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Application.Interfaces.Services.Dashboard;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = PermissionNames.EnviosConsultar)]
public sealed class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<IActionResult> Obtener([FromQuery] int meses = 12, CancellationToken cancellationToken = default) =>
        (await service.ObtenerAsync(meses, cancellationToken)).ToActionResult(this);
}

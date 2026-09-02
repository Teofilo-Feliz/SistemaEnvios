using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Api.Extensions;
using SistemaEnvios.Api.Security;
using SistemaEnvios.Application.Interfaces.Services.Seguridad;

namespace SistemaEnvios.Api.Controllers.Seguridad;

[ApiController]
[Route("api/perfil")]
public sealed class PerfilController(IPerfilUsuarioService service) : ControllerBase
{
    /// <summary>
    /// Quién es el usuario y qué alcance tiene. No exige permiso: es la respuesta a "quién soy",
    /// y el frontend la necesita antes de saber qué puede pedir. La FallbackPolicy de Program.cs
    /// sigue exigiendo que esté autenticado.
    /// </summary>
    [HttpGet]
    [AutenticacionSuficiente]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken) =>
        (await service.ObtenerAsync(cancellationToken)).ToActionResult(this);
}

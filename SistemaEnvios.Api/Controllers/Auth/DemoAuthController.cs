using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public sealed class DemoAuthController(IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("demo-login")]
    public IActionResult Login([FromBody] DemoLoginRequest request)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoAuth:Enabled")) return NotFound();
        if (request.Username != "demo" || request.Password != "demo123") return Unauthorized(new { message = "Usuario o contraseña incorrectos." });
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "11111111-1111-1111-1111-111111111111"), new("name", "Usuario Demo"), new("roles", "Administrador") };
        foreach (var permission in new[] { PermissionNames.EnviosConsultar, PermissionNames.EnviosCrear, PermissionNames.EnviosEditar, PermissionNames.EnviosDespachar, PermissionNames.TransportesGestionar, PermissionNames.TransportesConfirmar, PermissionNames.RecepcionesGestionar, PermissionNames.IncidenciasGestionar, PermissionNames.EquiposGestionar, PermissionNames.CatalogosAdministrar }) claims.Add(new Claim("permissions", permission));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["DemoAuth:SigningKey"]!));
        var token = new JwtSecurityToken(configuration["DemoAuth:Issuer"], configuration["DemoAuth:Audience"], claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), user = new { name = "Usuario Demo", role = "Administrador de demostración", initials = "UD" }, permissions = claims.Where(x => x.Type == "permissions").Select(x => x.Value) });
    }
}

public sealed record DemoLoginRequest(string Username, string Password);

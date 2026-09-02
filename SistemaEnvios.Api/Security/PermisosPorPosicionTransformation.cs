using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Api.Security;

/// <summary>
/// AuthManager emite la posición del usuario ("Asistente Administrativo") y permisos de otras
/// aplicaciones ("evaluador"), no los permisos de este sistema. Esta transformación deriva los
/// permisos propios desde PermisosPorPosicion y los inyecta como claims "permissions", que es
/// de donde AuthorizationConfiguration los lee. Corre después de autenticar y antes de
/// autorizar, así que las políticas [Authorize] y IUserContext ven lo mismo.
/// </summary>
public sealed class PermisosPorPosicionTransformation(SistemaEnviosDbContext db, IMemoryCache cache)
    : IClaimsTransformation
{
    private const string ClaimPermisos = "permissions";
    private const string ClaimPosicion = "position";
    private const string MarcaAplicada = "sistema_envios_permisos_resueltos";
    private static readonly TimeSpan VigenciaCache = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> PermisosConocidos = typeof(PermissionNames)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(x => x.IsLiteral && x.FieldType == typeof(string))
        .Select(x => (string)x.GetRawConstantValue()!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // ASP.NET Core puede invocarla más de una vez por petición.
        if (principal.HasClaim(MarcaAplicada, "1")) return principal;
        if (principal.Identity is not ClaimsIdentity identidad || !identidad.IsAuthenticated) return principal;

        // Del token solo sobreviven los permisos que existen en este sistema: "evaluador"
        // pertenece a otra aplicación y no debe otorgar nada aquí.
        var permisos = principal.FindAll(ClaimPermisos)
            .SelectMany(x => x.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(PermisosConocidos.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var posicion = principal.FindFirstValue(ClaimPosicion)?.Trim();
        if (!string.IsNullOrEmpty(posicion))
            permisos.UnionWith(await PermisosDePosicionAsync(posicion));

        var resultado = principal.Clone();
        var destino = (ClaimsIdentity)resultado.Identity!;
        foreach (var claim in destino.FindAll(ClaimPermisos).ToArray())
            destino.RemoveClaim(claim);
        foreach (var permiso in permisos)
            destino.AddClaim(new Claim(ClaimPermisos, permiso));
        destino.AddClaim(new Claim(MarcaAplicada, "1"));
        return resultado;
    }

    private async Task<IReadOnlyCollection<string>> PermisosDePosicionAsync(string posicion)
    {
        var clave = $"permisos-posicion::{posicion.ToUpperInvariant()}";
        if (cache.TryGetValue(clave, out IReadOnlyCollection<string>? cacheado) && cacheado is not null)
            return cacheado;

        var permisos = await db.PermisosPorPosicion.AsNoTracking()
            .Where(x => x.Posicion == posicion)
            .Select(x => x.Permiso)
            .ToArrayAsync();

        cache.Set(clave, (IReadOnlyCollection<string>)permisos, VigenciaCache);
        return permisos;
    }
}

using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Seguridad;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services.Seguridad;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Seguridad;

public sealed class PerfilUsuarioService(
    SistemaEnviosDbContext db,
    IUserContext usuario,
    IAlcanceEnvios alcance) : IPerfilUsuarioService
{
    public async Task<Result<PerfilUsuarioResponse>> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (!usuario.IsAuthenticated)
            return Result<PerfilUsuarioResponse>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var perfil = await alcance.ResolverPerfilAsync(cancellationToken);
        var filialId = usuario.AffiliateId;

        var ubicacion = filialId is int id
            ? await db.Ubicaciones.AsNoTracking()
                .Where(x => x.FilialExternaId == id)
                .Select(x => new { x.UbicacionId, x.Nombre })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return Result<PerfilUsuarioResponse>.Success(new PerfilUsuarioResponse(
            Perfil: perfil.ToString(),
            // Las dos claves con las que se resolvió el alcance, para poder ver desde el navegador
            // por qué un usuario aterrizó donde aterrizó.
            Posicion: usuario.Position,
            Roles: usuario.Roles,
            FilialId: filialId,
            FilialNombre: ubicacion?.Nombre,
            UbicacionId: ubicacion?.UbicacionId,
            FilialMapeada: ubicacion is not null,
            // Solo el perfil global elige filial: los demás ya vienen acotados por el backend.
            PuedeFiltrarPorFilial: perfil.EsTecnologia(),
            Permisos: usuario.Permissions));
    }
}

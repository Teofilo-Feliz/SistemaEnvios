using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Notificaciones;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Notificaciones;

public sealed class NotificacionService(SistemaEnviosDbContext db, IUnitOfWork uow, IUserContext users) : INotificacionService
{
    public async Task<Result<PaginaResponse<NotificacionResponse>>> ListarAsync(ConsultarNotificacionesRequest request, CancellationToken ct = default)
    {
        var q = db.Notificaciones.AsNoTracking().Where(x => x.DestinatarioRol == request.Rol);
        if (request.Rol.Equals("FILIAL", StringComparison.OrdinalIgnoreCase) && users.AffiliateId.HasValue)
            q = q.Where(x => x.Envio.UbicacionDestino.FilialExternaId == users.AffiliateId.Value);
        if (request.SoloNoLeidas) q = q.Where(x => x.FechaLeida == null);

        // Antes se cortaba con Take(100) fijo: el usuario no sabía que había más ni podía llegar
        // a ellas. Ahora el corte es una página y el total dice cuántas quedan.
        var pagina = await q.OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.NotificacionId)
            .PaginarAsync(request, x => new NotificacionResponse(x.NotificacionId, x.EnvioId, x.Envio.NumeroEnvio, x.Tipo, x.Titulo, x.Mensaje, x.DestinatarioRol, x.FechaCreacion, x.FechaLeida), ct);
        return Result<PaginaResponse<NotificacionResponse>>.Success(pagina);
    }

    public async Task<Result> MarcarLeidaAsync(long id, CancellationToken ct = default)
    {
        if (users.UserId is not Guid u) return Result.Failure("Usuario no identificado.", ErrorType.Unauthorized);
        var x = await db.Notificaciones.Include(n => n.Envio).ThenInclude(e => e.UbicacionDestino).FirstOrDefaultAsync(n => n.NotificacionId == id, ct);
        if (x is null) return Result.Failure("La notificación no existe.", ErrorType.NotFound);
        if (x.DestinatarioRol.Equals("FILIAL", StringComparison.OrdinalIgnoreCase) && users.AffiliateId.HasValue && x.Envio.UbicacionDestino.FilialExternaId != users.AffiliateId.Value) return Result.Failure("No tiene acceso a esta notificación.", ErrorType.Forbidden);
        x.FechaLeida ??= DateTime.UtcNow; x.UsuarioLecturaId ??= u;
        await uow.SaveChangesAsync(ct); return Result.Success();
    }
}

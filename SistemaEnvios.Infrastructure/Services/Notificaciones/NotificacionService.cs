using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Notificaciones;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Infrastructure.Persistence;
namespace SistemaEnvios.Infrastructure.Services.Notificaciones;
public sealed class NotificacionService(SistemaEnviosDbContext db,IUnitOfWork uow,IUserContext users):INotificacionService
{
    public async Task<Result<IReadOnlyCollection<NotificacionResponse>>> ListarAsync(string rol,bool soloNoLeidas=true,CancellationToken ct=default){var q=db.Notificaciones.AsNoTracking().Where(x=>x.DestinatarioRol==rol);if(soloNoLeidas)q=q.Where(x=>x.FechaLeida==null);var rows=await q.OrderByDescending(x=>x.FechaCreacion).Take(100).Select(x=>new NotificacionResponse(x.NotificacionId,x.EnvioId,x.Envio.NumeroEnvio,x.Tipo,x.Titulo,x.Mensaje,x.DestinatarioRol,x.FechaCreacion,x.FechaLeida)).ToListAsync(ct);return Result<IReadOnlyCollection<NotificacionResponse>>.Success(rows);}
    public async Task<Result> MarcarLeidaAsync(long id,CancellationToken ct=default){if(users.UserId is not Guid u)return Result.Failure("Usuario no identificado.",ErrorType.Unauthorized);var x=await db.Notificaciones.FindAsync([id],ct);if(x is null)return Result.Failure("La notificación no existe.",ErrorType.NotFound);x.FechaLeida??=DateTime.UtcNow;x.UsuarioLecturaId??=u;await uow.SaveChangesAsync(ct);return Result.Success();}
}

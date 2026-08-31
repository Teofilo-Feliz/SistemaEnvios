using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Notificaciones;
namespace SistemaEnvios.Application.Interfaces.Services;
public interface INotificacionService
{
    Task<Result<IReadOnlyCollection<NotificacionResponse>>> ListarAsync(string rol,bool soloNoLeidas=true,CancellationToken ct=default);
    Task<Result> MarcarLeidaAsync(long id,CancellationToken ct=default);
}

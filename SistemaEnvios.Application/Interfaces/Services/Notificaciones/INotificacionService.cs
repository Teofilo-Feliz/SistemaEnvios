using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Notificaciones;
namespace SistemaEnvios.Application.Interfaces.Services;
public interface INotificacionService
{
    Task<Result<PaginaResponse<NotificacionResponse>>> ListarAsync(ConsultarNotificacionesRequest request,CancellationToken ct=default);
}

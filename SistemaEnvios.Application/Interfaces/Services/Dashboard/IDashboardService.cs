using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Dashboard;

namespace SistemaEnvios.Application.Interfaces.Services.Dashboard;

public interface IDashboardService
{
    Task<Result<DashboardResponse>> ObtenerAsync(int meses = 12, CancellationToken cancellationToken = default);

    /// <summary>Totales de las etapas de Transportación, agregados en la base.</summary>
    Task<Result<DashboardTransportacionResponse>> ObtenerTransportacionAsync(CancellationToken cancellationToken = default);
}

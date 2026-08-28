using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEstadoEnvioService
{
    Task<Result<EstadoEnvioResponse>> ObtenerAsync(int estadoId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<EstadoEnvioResponse>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result> CambiarAsync(CambiarEstadoEnvioRequest request, CancellationToken cancellationToken = default);
}

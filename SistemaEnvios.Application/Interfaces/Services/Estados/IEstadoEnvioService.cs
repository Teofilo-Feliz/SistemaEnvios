using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEstadoEnvioService
{
    Task<Result<int>> CrearAsync(EstadoEnvio estado, CancellationToken cancellationToken = default);
    Task<Result<EstadoEnvio>> ObtenerAsync(int estadoId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<EstadoEnvio>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(EstadoEnvio estado, CancellationToken cancellationToken = default);
    Task<Result> CambiarActivoAsync(int estadoId, bool activo, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<Result> CambiarAsync(int envioId, int estadoDestinoId, Guid usuarioId, int ubicacionId, string? observaciones = null, CancellationToken cancellationToken = default);
}

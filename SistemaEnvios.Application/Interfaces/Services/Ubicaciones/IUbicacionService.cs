using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IUbicacionService
{
    Task<Result<int>> CrearAsync(Ubicacion ubicacion, CancellationToken cancellationToken = default);
    Task<Result<Ubicacion>> ObtenerAsync(int ubicacionId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<Ubicacion>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(Ubicacion ubicacion, CancellationToken cancellationToken = default);
    Task<Result> CambiarActivoAsync(int ubicacionId, bool activo, Guid usuarioId, CancellationToken cancellationToken = default);
}

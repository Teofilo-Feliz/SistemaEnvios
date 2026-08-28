using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Ubicaciones;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IUbicacionService
{
    Task<Result<int>> CrearAsync(GuardarUbicacionRequest request, CancellationToken cancellationToken = default);
    Task<Result<UbicacionResponse>> ObtenerAsync(int ubicacionId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<UbicacionResponse>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(GuardarUbicacionRequest request, CancellationToken cancellationToken = default);
    Task<Result> CambiarActivoAsync(int ubicacionId, bool activo, CancellationToken cancellationToken = default);
}

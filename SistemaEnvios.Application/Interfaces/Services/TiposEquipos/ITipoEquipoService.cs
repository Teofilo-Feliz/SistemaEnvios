using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITipoEquipoService
{
    Task<Result<int>> CrearAsync(TipoEquipo tipoEquipo, CancellationToken cancellationToken = default);
    Task<Result<TipoEquipo>> ObtenerAsync(int tipoEquipoId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TipoEquipo>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(TipoEquipo tipoEquipo, CancellationToken cancellationToken = default);
    Task<Result> CambiarActivoAsync(int tipoEquipoId, bool activo, Guid usuarioId, CancellationToken cancellationToken = default);
}

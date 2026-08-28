using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEquipoService
{
    Task<Result<int>> CrearAsync(Equipo equipo, CancellationToken cancellationToken = default);
    Task<Result<Equipo>> ObtenerAsync(int equipoId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<Equipo>>> ListarAsync(CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(Equipo equipo, CancellationToken cancellationToken = default);
}

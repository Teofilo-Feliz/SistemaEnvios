using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITransicionEstadoEnvioService
{
    Task<Result<int>> CrearAsync(TransicionEstadoEnvio transicion, CancellationToken cancellationToken = default);
    Task<Result<TransicionEstadoEnvio>> ObtenerAsync(int transicionId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TransicionEstadoEnvio>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default);
    Task<Result> CambiarActivoAsync(int transicionId, bool activo, CancellationToken cancellationToken = default);
}

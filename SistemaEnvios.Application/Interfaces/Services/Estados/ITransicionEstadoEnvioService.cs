using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITransicionEstadoEnvioService
{
    Task<Result<TransicionEstadoResponse>> ObtenerAsync(int transicionId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TransicionEstadoResponse>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default);
}

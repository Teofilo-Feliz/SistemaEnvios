using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IHistorialEstadoEnvioService
{
    Task<Result<IReadOnlyCollection<HistorialEstadoEnvioResponse>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
}

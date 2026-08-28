using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IHistorialEstadoEnvioService
{
    Task<Result<IReadOnlyCollection<HistorialEstadoEnvio>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
}

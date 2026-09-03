using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Estados;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IHistorialEstadoEnvioService
{
    Task<Result<PaginaResponse<HistorialEstadoEnvioResponse>>> ListarPorEnvioAsync(int envioId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default);
}

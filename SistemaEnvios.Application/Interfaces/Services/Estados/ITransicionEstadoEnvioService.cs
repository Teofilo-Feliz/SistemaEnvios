using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Estados;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITransicionEstadoEnvioService
{
    Task<Result<TransicionEstadoResponse>> ObtenerAsync(int transicionId, CancellationToken cancellationToken = default);
    Task<Result<PaginaResponse<TransicionEstadoResponse>>> ListarAsync(ConsultarCatalogoRequest request, CancellationToken cancellationToken = default);
}

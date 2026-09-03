using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Envios;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEnvioService
{
    Task<Result<EnvioResponse>> CrearAsync(CrearEnvioRequest request, CancellationToken cancellationToken = default);
    Task<Result<EnvioResponse>> CrearConEquiposAsync(CrearEnvioConEquiposRequest request, CancellationToken cancellationToken = default);
    Task<Result<EnvioResponse>> ObtenerAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result<PaginaResponse<EnvioResponse>>> ConsultarAsync(ConsultarEnviosRequest request, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(ActualizarEnvioRequest request, CancellationToken cancellationToken = default);
}

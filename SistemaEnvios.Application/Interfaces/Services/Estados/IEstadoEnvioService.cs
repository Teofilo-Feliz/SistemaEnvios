using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Estados;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEstadoEnvioService
{
    Task<Result<EstadoEnvioResponse>> ObtenerAsync(int estadoId, CancellationToken cancellationToken = default);
    Task<Result<PaginaResponse<EstadoEnvioResponse>>> ListarAsync(ConsultarCatalogoRequest request, CancellationToken cancellationToken = default);
    Task<Result> CambiarAsync(CambiarEstadoEnvioRequest request, CancellationToken cancellationToken = default);
    Task<Result> EntregarATransportacionAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default);
    Task<Result> EntregarTransportePrivadoAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default);
    Task<Result> RegistrarLlegadaTecnologiaAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default);
    Task<Result> ConfirmarLlegadaTransportacionAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default);
    Task<Result> DespacharDesdeTecnologiaAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default);
}

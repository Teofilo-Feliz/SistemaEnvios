using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Transportes;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITransporteService
{
    Task<Result<int>> CrearAsync(CrearTransporteRequest request, CancellationToken cancellationToken = default);
    Task<Result<TransporteResponse>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);

    /// <summary>Envíos entregados a Transportación cuyo chofer aún no confirmó que los recibió.</summary>
    Task<Result<PaginaResponse<BandejaTransportacionResponse>>> ListarPendientesDeCustodiaAsync(ParametrosPaginaSimple request, CancellationToken cancellationToken = default);

    /// <summary>Envíos que ya salieron a ruta y esperan que se confirme su llegada.</summary>
    Task<Result<PaginaResponse<BandejaTransportacionResponse>>> ListarPendientesDeLlegadaAsync(ParametrosPaginaSimple request, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(ActualizarTransporteRequest request, CancellationToken cancellationToken = default);
    Task<Result> ConfirmarAsync(int transporteId, CancellationToken cancellationToken = default);
}

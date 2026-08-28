using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Transportes;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITransporteService
{
    Task<Result<int>> CrearAsync(CrearTransporteRequest request, CancellationToken cancellationToken = default);
    Task<Result<TransporteResponse>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(ActualizarTransporteRequest request, CancellationToken cancellationToken = default);
    Task<Result> ConfirmarAsync(int transporteId, CancellationToken cancellationToken = default);
}

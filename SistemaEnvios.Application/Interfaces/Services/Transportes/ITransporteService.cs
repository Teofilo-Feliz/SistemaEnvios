using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Domain.Entities;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITransporteService
{
    Task<Result<int>> CrearAsync(CrearTransporteRequest request, CancellationToken cancellationToken = default);
    Task<Result<Transporte>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result> ConfirmarAsync(int transporteId, Guid usuarioId, CancellationToken cancellationToken = default);
}

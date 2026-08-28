using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Domain.Entities;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IRecepcionService
{
    Task<Result<int>> CrearAsync(CrearRecepcionRequest request, CancellationToken cancellationToken = default);
    Task<Result<Recepcion>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result> VerificarEquipoAsync(VerificarEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result> CompletarAsync(int recepcionId, Guid usuarioId, CancellationToken cancellationToken = default);
}

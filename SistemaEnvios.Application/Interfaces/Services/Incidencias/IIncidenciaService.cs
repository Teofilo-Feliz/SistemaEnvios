using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Incidencias;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IIncidenciaService
{
    Task<Result<int>> RegistrarAsync(
        CrearIncidenciaRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<IncidenciaResponse>> ObtenerAsync(int incidenciaId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<IncidenciaResponse>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
}

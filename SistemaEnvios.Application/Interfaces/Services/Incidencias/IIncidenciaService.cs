using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Incidencias;
using SistemaEnvios.Domain.Entities;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IIncidenciaService
{
    Task<Result<int>> RegistrarAsync(
        CrearIncidenciaRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<Incidencia>> ObtenerAsync(int incidenciaId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<Incidencia>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
}

using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Incidencias;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IIncidenciaService
{
    Task<Result<int>> RegistrarAsync(
        CrearIncidenciaRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<IncidenciaResponse>> ObtenerAsync(int incidenciaId, CancellationToken cancellationToken = default);
    Task<Result<PaginaResponse<IncidenciaResponse>>> ListarPorEnvioAsync(int envioId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default);

    /// <summary>Listado general y paginado, para no pedir una consulta de incidencias por envío.</summary>
    Task<Result<PaginaResponse<IncidenciaListadoResponse>>> ListarAsync(ConsultarIncidenciasRequest request, CancellationToken cancellationToken = default);
}

using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Equipos;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEquipoService
{
    Task<Result<int>> CrearAsync(CrearEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result<EquipoResponse>> ObtenerAsync(int equipoId, CancellationToken cancellationToken = default);
    Task<Result<PaginaResponse<EquipoResponse>>> ListarAsync(ConsultarEquiposRequest request, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(ActualizarEquipoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Por dónde ha pasado el equipo. Paginado como todo lo demás: un equipo con años de vida
    /// acumula viajes, y traerlos todos de golpe castiga a la base sin que nadie los lea.
    /// </summary>
    Task<Result<PaginaResponse<ViajeEquipoResponse>>> ListarViajesAsync(int equipoId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default);
}

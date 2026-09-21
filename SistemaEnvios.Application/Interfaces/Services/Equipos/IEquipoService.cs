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
    /// Si el código de activo está libre en TODA la base.
    /// </summary>
    /// <remarks>
    /// Va contra la base y no contra el inventario que el formulario ya tiene cargado, porque ese
    /// solo trae los equipos de la ubicación de origen: un código puede pertenecer a un equipo de
    /// otra filial y no aparecería.
    ///
    /// Es la misma regla que aplica <c>CrearAsync</c> al guardar; esto solo la adelanta para que
    /// el usuario se entere antes de llenar el formulario entero.
    /// </remarks>
    Task<Result<bool>> CodigoActivoDisponibleAsync(string codigoActivo, int? excluirEquipoId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Por dónde ha pasado el equipo. Paginado como todo lo demás: un equipo con años de vida
    /// acumula viajes, y traerlos todos de golpe castiga a la base sin que nadie los lea.
    /// </summary>
    Task<Result<PaginaResponse<ViajeEquipoResponse>>> ListarViajesAsync(int equipoId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default);
}

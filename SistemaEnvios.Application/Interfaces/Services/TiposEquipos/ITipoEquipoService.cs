using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.TiposEquipos;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface ITipoEquipoService
{
    Task<Result<int>> CrearAsync(GuardarTipoEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result<TipoEquipoResponse>> ObtenerAsync(int tipoEquipoId, CancellationToken cancellationToken = default);
    Task<Result<PaginaResponse<TipoEquipoResponse>>> ListarAsync(ConsultarCatalogoRequest request, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(GuardarTipoEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result> CambiarActivoAsync(int tipoEquipoId, bool activo, CancellationToken cancellationToken = default);
}

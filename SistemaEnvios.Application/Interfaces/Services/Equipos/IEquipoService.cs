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
}

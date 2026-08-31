using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Transportes;
namespace SistemaEnvios.Application.Interfaces.Services;
public interface ICatalogoTransporteService
{
    Task<Result<IReadOnlyCollection<TipoTransporteResponse>>> ListarTiposAsync(bool soloActivos = true, CancellationToken ct = default);
    Task<Result<int>> CrearTipoAsync(GuardarTipoTransporteRequest request, CancellationToken ct = default);
    Task<Result> ActualizarTipoAsync(GuardarTipoTransporteRequest request, CancellationToken ct = default);
    Task<Result> CambiarTipoActivoAsync(int id, bool activo, CancellationToken ct = default);
    Task<Result<IReadOnlyCollection<ChoferInternoResponse>>> ListarChoferesAsync(bool soloActivos = true, CancellationToken ct = default);
    Task<Result<int>> CrearChoferAsync(GuardarChoferInternoRequest request, CancellationToken ct = default);
    Task<Result> ActualizarChoferAsync(GuardarChoferInternoRequest request, CancellationToken ct = default);
    Task<Result> CambiarChoferActivoAsync(int id, bool activo, CancellationToken ct = default);
}

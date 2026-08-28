using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEnvioEquipoService
{
    Task<Result<int>> AgregarAsync(AgregarEquipoEnvioRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<EnvioEquipoResponse>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(ActualizarEnvioEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result> QuitarAsync(int envioEquipoId, CancellationToken cancellationToken = default);
}

using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Domain.Entities;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IEnvioEquipoService
{
    Task<Result<int>> AgregarAsync(AgregarEquipoEnvioRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<EnvioEquipo>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
}

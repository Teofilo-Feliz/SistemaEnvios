using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Repositories;

public interface IEnvioEquipoRepository
{
    Task<bool> ExisteEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<bool> ExisteEquipoAsync(int equipoId, CancellationToken cancellationToken = default);
    Task<bool> ExisteEnEnvioAsync(int envioId, int equipoId, CancellationToken cancellationToken = default);
    Task AgregarAsync(EnvioEquipo envioEquipo, CancellationToken cancellationToken = default);
}

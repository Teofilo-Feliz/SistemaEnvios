using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Seguridad;

namespace SistemaEnvios.Application.Interfaces.Services.Seguridad;

public interface IPerfilUsuarioService
{
    Task<Result<PerfilUsuarioResponse>> ObtenerAsync(CancellationToken cancellationToken = default);
}

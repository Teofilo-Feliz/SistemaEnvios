using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;

namespace SistemaEnvios.Application.Interfaces.Services.Integraciones;

/// <summary>
/// Trae de la mesa de ayuda los datos del equipo de un ticket, para autocompletar el formulario.
/// </summary>
public interface IEquipoDeTicketGlpi
{
    /// <summary>
    /// Falla con <see cref="ErrorType.Validation"/> por los mismos motivos que
    /// <see cref="IValidadorTicketGlpi"/> —ticket inexistente, o con más de un equipo— para que
    /// el usuario lea el mismo mensaje aquí que al guardar.
    /// </summary>
    Task<Result<EquipoDeTicketResponse>> ObtenerAsync(string numeroTicket, CancellationToken ct = default);
}

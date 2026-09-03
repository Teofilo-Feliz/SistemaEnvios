using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Application.DTOs.Notificaciones;

public sealed class ConsultarNotificacionesRequest : ParametrosPagina
{
    public string Rol { get; init; } = "TECNOLOGIA";
}

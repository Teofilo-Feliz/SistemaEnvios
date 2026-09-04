namespace SistemaEnvios.Application.DTOs.Transportes;

/// <summary>
/// Fila de una bandeja de Transportación. Cada bandeja atiende un momento distinto —que el
/// chofer recibió el equipo, o que el envío llegó— y trae ya resuelto lo que hace falta para
/// decidir, sin una consulta de transporte por cada envío.
/// </summary>
public sealed record BandejaTransportacionResponse(
    int EnvioId,
    int TransporteId,
    string NumeroEnvio,
    string NombreTipoTransporte,
    string NombreChofer,
    string NumeroEmpleadoChofer,
    string UbicacionOrigen,
    string UbicacionDestino,
    DateTime? FechaEntrega);

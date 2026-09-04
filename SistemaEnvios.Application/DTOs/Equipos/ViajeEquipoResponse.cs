namespace SistemaEnvios.Application.DTOs.Equipos;

/// <summary>
/// Un viaje del equipo: una fila de su historial. El historial es del equipo y no del envío,
/// porque un envío mueve varios equipos y cada uno sigue su propio camino después.
/// </summary>
/// <param name="NumeroTicket">
/// Ticket del caso al que pertenece el viaje. Se repite entre viajes a propósito: todas las
/// vueltas de un mismo caso comparten el ticket que lo abrió.
/// </param>
/// <param name="EsAperturaDeCaso">
/// Si este viaje estrenó el ticket o lo heredó de uno anterior. Es lo que convierte una lista
/// de viajes en una historia: sin esto el mismo número aparece repetido sin explicar por qué.
/// </param>
public sealed record ViajeEquipoResponse(
    int EnvioId,
    string NumeroEnvio,
    DateTime Fecha,
    string Origen,
    string Destino,
    string EstadoCodigo,
    string EstadoNombre,
    string NumeroTicket,
    bool EsAperturaDeCaso,
    string Observaciones);

namespace SistemaEnvios.Application.DTOs.Casos;

/// <summary>
/// El caso abierto de un equipo. Es lo que decide si un movimiento estrena ticket o hereda el
/// que ya existe, y a qué filial tiene que volver el equipo.
/// </summary>
/// <param name="EnvioEquipoAperturaId">Movimiento que abrió el caso; raíz de la cadena.</param>
/// <param name="NumeroTicket">Ticket del caso. Lo heredan todas las continuaciones.</param>
/// <param name="FilialId">Ubicación de la filial dueña del caso: el único destino válido para la devolución.</param>
/// <param name="Movimientos">
/// Cuántos movimientos lleva el caso. Un caso con muchas vueltas es algo que no se está
/// resolviendo, y conviene que se vea en vez de quedar enterrado en el historial.
/// </param>
public sealed record CasoAbiertoResponse(
    int EnvioEquipoAperturaId,
    string NumeroTicket,
    int FilialId,
    int Movimientos,
    DateTime FechaApertura);

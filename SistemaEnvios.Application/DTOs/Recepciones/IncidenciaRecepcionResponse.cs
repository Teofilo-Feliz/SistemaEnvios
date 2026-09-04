namespace SistemaEnvios.Application.DTOs.Recepciones;

/// <summary>
/// Un equipo que llegó mal, con lo que anotó quien lo recibió. Saber que un envío tiene
/// incidencia no sirve si no se puede ver cuál equipo fue y qué le pasa; lleva el ticket para
/// poder seguir el caso, que es lo que continúa abierto.
/// </summary>
public sealed record IncidenciaRecepcionResponse(
    int EnvioEquipoId,
    int EquipoId,
    string? NumeroSerie,
    string? CodigoActivo,
    string Marca,
    string Modelo,
    string NumeroTicket,
    string? Observaciones,
    DateTime? FechaVerificacion);

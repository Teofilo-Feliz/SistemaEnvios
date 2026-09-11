namespace SistemaEnvios.Application.DTOs.Integraciones;

/// <summary>
/// Lo que el formulario necesita para autocompletar un equipo a partir de su ticket.
/// </summary>
/// <remarks>
/// <see cref="Equipo"/> viene en null cuando el ticket existe pero no tiene ningún activo
/// asociado, que es un caso válido: la persona escribe los datos a mano. Un ticket con varios no
/// llega hasta aquí, lo rechaza antes la política de un equipo por ticket.
/// </remarks>
public sealed record EquipoDeTicketResponse(int Ticket, EquipoAutocompletado? Equipo);

public sealed record EquipoAutocompletado(
    string? Marca,
    string? Modelo,
    string? Serial,
    string? CodigoActivo,
    int? TipoEquipoId,
    string? Nombre,
    bool YaEstaEnOtroEnvio);

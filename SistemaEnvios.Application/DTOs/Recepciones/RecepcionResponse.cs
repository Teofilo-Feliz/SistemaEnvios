using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.DTOs.Recepciones;

public sealed record RecepcionResponse(
    int RecepcionId,
    int EnvioId,
    Guid? TecnicoAsignadoUsuarioId,
    string? TecnicoAsignadoNombre,
    string? TecnicoAsignadoNumeroEmpleado,
    Guid? UsuarioQueRecibioId,
    DateTime? FechaAsignacion,
    DateTime? FechaRecepcion,
    EstadoRecepcionEnum EstadoRecepcion,
    string? Observaciones,
    Guid UsuarioQueAsignoId);

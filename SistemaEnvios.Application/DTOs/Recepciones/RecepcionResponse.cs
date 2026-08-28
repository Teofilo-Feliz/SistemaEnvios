using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.DTOs.Recepciones;

public sealed record RecepcionResponse(
    int RecepcionId,
    int EnvioId,
    int? TecnicoAsignadoId,
    Guid? UsuarioQueRecibioId,
    DateTime? FechaAsignacion,
    DateTime? FechaRecepcion,
    EstadoRecepcionEnum EstadoRecepcion,
    string? Observaciones,
    Guid UsuarioQueAsignoId);

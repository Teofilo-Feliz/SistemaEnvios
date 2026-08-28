using SistemaEnvios.Domain.Enums;
namespace SistemaEnvios.Application.DTOs.Recepciones;

public sealed class VerificarEquipoRequest { public int RecepcionId { get; init; } public int EnvioEquipoId { get; init; } public EstadoRecepcionEquipoEnum Estado { get; init; } public string? Observaciones { get; init; } }

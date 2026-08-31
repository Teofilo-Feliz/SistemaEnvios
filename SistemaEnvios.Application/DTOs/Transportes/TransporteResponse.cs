using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.DTOs.Transportes;

public sealed record TransporteResponse(
    int TransporteId, int EnvioId, int TipoTransporteId, string CodigoTipo, string NombreTipo,
    EstrategiaTransporteEnum Estrategia, int? ChoferInternoId, string? NombreChofer,
    string? NumeroEmpleado, string? NombreResponsable, string? Parentesco,
    string? CedulaResponsable, string? PlacaVehiculo, DateTime? FechaEntrega,
    string? Observaciones, bool EntregaConfirmada, DateTime? FechaConfirmacionEntrega,
    Guid? UsuarioConfirmacionId);

namespace SistemaEnvios.Application.DTOs.Transportes;

public sealed record TransporteResponse(
    int TransporteId,
    int EnvioId,
    string Tipo,
    string? NombreChofer,
    string? Placa,
    DateTime? FechaEntregaTransportacion,
    string? Observaciones,
    bool EntregaConfirmada,
    DateTime? FechaConfirmacionEntrega,
    Guid? UsuarioConfirmacionId);

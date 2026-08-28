namespace SistemaEnvios.Application.DTOs.Envios;

public sealed record EnvioEquipoResponse(
    int EnvioEquipoId,
    int EnvioId,
    int EquipoId,
    string NumeroTicket,
    Guid UsuarioSolicitanteId,
    string Observaciones);

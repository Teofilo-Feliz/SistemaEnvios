namespace SistemaEnvios.Application.DTOs.Envios;

public sealed record EnvioEquipoResponse(
    int EnvioEquipoId,
    int EnvioId,
    int EquipoId,
    string NumeroTicket,
    Guid UsuarioSolicitanteId,
    string Observaciones,
    /// <summary>
    /// Movimiento que abrió el caso, o null si este lo abre. La pantalla lo usa para saber que
    /// el ticket viene heredado y no debe validarlo ni dejar que lo cambien.
    /// </summary>
    int? EnvioEquipoOrigenId);

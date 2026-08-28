namespace SistemaEnvios.Application.DTOs.Envios;

public sealed class AgregarEquipoEnvioRequest
{
    public int EnvioId { get; init; }
    public int EquipoId { get; init; }
    public string NumeroTicket { get; init; } = null!;
    public Guid UsuarioSolicitanteId { get; init; }
    public string Observaciones { get; init; } = null!;
}

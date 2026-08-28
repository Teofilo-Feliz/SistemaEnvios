namespace SistemaEnvios.Application.DTOs.Envios;

public sealed class CrearEnvioRequest
{
    public int UbicacionOrigenId { get; init; }
    public int UbicacionDestinoId { get; init; }
    public int EstadoEnvioId { get; init; }
    public Guid UsuarioSolicitanteId { get; init; }
    public string? Observaciones { get; init; }
}

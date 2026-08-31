namespace SistemaEnvios.Application.DTOs.Envios;
public sealed class CrearEnvioConEquiposRequest
{
    public int UbicacionOrigenId { get; init; }
    public int UbicacionDestinoId { get; init; }
    public string? Observaciones { get; init; }
    public IReadOnlyCollection<EquipoEnvioInicialRequest> Equipos { get; init; } = [];
}
public sealed class EquipoEnvioInicialRequest
{
    public int EquipoId { get; init; }
    public string NumeroTicket { get; init; } = null!;
    public string? Observaciones { get; init; }
}

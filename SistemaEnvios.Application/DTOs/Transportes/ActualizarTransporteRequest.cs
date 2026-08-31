namespace SistemaEnvios.Application.DTOs.Transportes;

public sealed class ActualizarTransporteRequest
{
    public int TransporteId { get; init; }
    public int TipoTransporteId { get; init; }
    public int? ChoferInternoId { get; init; }
    public string? NombreResponsable { get; init; }
    public string? Parentesco { get; init; }
    public string? CedulaResponsable { get; init; }
    public string? PlacaVehiculo { get; init; }
    public string? Observaciones { get; init; }
}

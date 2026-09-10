using SistemaEnvios.Domain.Enums;
namespace SistemaEnvios.Application.DTOs.Transportes;

public sealed class CrearTransporteRequest
{
    public int EnvioId { get; init; }
    public int TipoTransporteId { get; init; }
    public int? ChoferInternoId { get; init; }
    public string? NombreResponsable { get; init; }
    public string? Parentesco { get; init; }
    public TipoDocumentoEnum? TipoDocumento { get; init; }
    public string? DocumentoResponsable { get; init; }
    public string? PlacaVehiculo { get; init; }
    public string? Observaciones { get; init; }
}

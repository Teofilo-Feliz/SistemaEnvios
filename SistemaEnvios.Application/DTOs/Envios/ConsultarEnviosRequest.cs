namespace SistemaEnvios.Application.DTOs.Envios;
public sealed class ConsultarEnviosRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public int? EstadoEnvioId { get; init; }
    public int? TipoTransporteId { get; init; }
    public int? UbicacionOrigenId { get; init; }
    public int? UbicacionDestinoId { get; init; }
    public int? Direccion { get; init; }
}

using SistemaEnvios.Application.DTOs.Common;
namespace SistemaEnvios.Application.DTOs.Envios;
public sealed class ConsultarEnviosRequest : ParametrosPagina
{
    public string? Search { get; init; }
    public int? EstadoEnvioId { get; init; }
    public int? TipoTransporteId { get; init; }
    public int? UbicacionOrigenId { get; init; }
    public int? UbicacionDestinoId { get; init; }
    /// <summary>Envíos que tocan esta ubicación en cualquiera de los dos extremos.</summary>
    public int? UbicacionId { get; init; }
    public int? Direccion { get; init; }

    /// <summary>
    /// Etapas concretas que interesan, por código. Cada pantalla operativa mira un puñado
    /// distinto; sin esto tenían que pedir todos los envíos y descartar en el navegador.
    /// </summary>
    public string[]? EstadoCodigos { get; init; }
}
